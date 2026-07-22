using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class PrayerSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PrayerSchedulerService> _logger;

        public PrayerSchedulerService(IServiceProvider services, ILogger<PrayerSchedulerService> logger)
        {
            _services = services;
            _logger   = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessAsync();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessAsync()
        {
            try
            {
                using var scope       = _services.CreateScope();
                var prayerRepo        = scope.ServiceProvider.GetRequiredService<PrayerRepository>();
                var pushService       = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

                var orgIds = (await prayerRepo.GetOrgsWithPrayerModuleAsync()).ToList();

                foreach (var orgId in orgIds)
                    await ProcessOrgAsync(orgId, prayerRepo, pushService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrayerScheduler] Unexpected error");
            }
        }

        private async Task ProcessOrgAsync(
            Guid orgId,
            PrayerRepository prayerRepo,
            PushNotificationService pushService)
        {
            try
            {
                var orgSettings = await prayerRepo.GetOrgSettingsAsync(orgId);
                var userSettings = (await prayerRepo.GetAllUserSettingsByOrgAsync(orgId))
                    .ToDictionary(u => u.UserId);
                var users = (await prayerRepo.GetUsersByOrgAsync(orgId)).ToList();

                // 1. Ensure today's records exist for every user (with their own effective timezone)
                foreach (var (userId, username) in users)
                {
                    userSettings.TryGetValue(userId, out var uSetting);
                    var effective = ResolveSettings(orgSettings, uSetting);

                    TimeZoneInfo tzi = SafeGetTz(effective.Timezone);
                    var localNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
                    var localDate = localNow.Date;
                    var times     = PrayerTimeCalculator.Calculate(effective, localDate);

                    await prayerRepo.EnsureTodayRecordsAsync(userId, username, orgId, localDate, times);
                }

                // 2. Process status transitions and reminders per user
                var reminders = (await prayerRepo.GetEnabledRemindersByOrgAsync(orgId))
                    .GroupBy(r => r.UserId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var (userId, username) in users)
                {
                    userSettings.TryGetValue(userId, out var uSetting);
                    var effective = ResolveSettings(orgSettings, uSetting);

                    TimeZoneInfo tzi = SafeGetTz(effective.Timezone);
                    var localNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
                    var localTime = localNow.TimeOfDay;
                    var localDate = localNow.Date;

                    var records = (await prayerRepo.GetPendingTransitionRecordsAsync(orgId, localDate))
                        .Where(r => r.UserId == userId)
                        .ToList();

                    reminders.TryGetValue(userId, out var userReminders);

                    foreach (var record in records)
                    {
                        await HandleStatusTransitionsAsync(record, localTime, prayerRepo, pushService);
                        if (userReminders is not null)
                            await HandleRemindersAsync(record, localTime, userReminders, pushService, prayerRepo);
                    }

                    // End-of-day summary at 10 PM local
                    if (localNow.Hour == 22 && localNow.Minute == 0)
                    {
                        bool endOfDayEnabled = userReminders?.Any(r =>
                            r.PrayerName == "All" && r.ReminderType == "EndOfDay" && r.IsEnabled) ?? false;

                        if (endOfDayEnabled)
                            await SendEndOfDaySummaryAsync(userId, orgId, localDate, prayerRepo, pushService);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrayerScheduler] Error processing org {OrgId}", orgId);
            }
        }

        private async Task HandleStatusTransitionsAsync(
            PrayerRecord record, TimeSpan localTime,
            PrayerRepository repo, PushNotificationService push)
        {
            // Upcoming/ReminderSent → Pending when prayer time reached
            if (record.Status is PrayerStatus.Upcoming or PrayerStatus.ReminderSent
                && localTime >= record.PrayerTime)
            {
                int rows = await repo.TransitionStatusAsync(record.Id, record.Status, PrayerStatus.Pending);
                if (rows > 0)
                {
                    await repo.LogStatusHistoryAsync(new PrayerStatusHistory
                    {
                        Id              = Guid.NewGuid(),
                        PrayerRecordId  = record.Id,
                        OldStatus       = record.Status,
                        NewStatus       = PrayerStatus.Pending,
                        ChangedByUsername = "System",
                        ChangedAt       = DateTime.UtcNow
                    });

                    await push.SendToSubscriptionsByUserAsync(record.UserId,
                        $"Time for {record.PrayerName}",
                        $"It's time for {record.PrayerName} prayer.",
                        "/prayer");
                }
                record.Status = PrayerStatus.Pending;
            }

            // Active → Missed when prayer window closes
            bool isActive = record.Status is PrayerStatus.Upcoming or PrayerStatus.ReminderSent or PrayerStatus.Pending;
            bool windowClosed = record.PrayerEndTime < TimeSpan.FromHours(24)
                ? localTime >= record.PrayerEndTime
                : false; // Isha — window closes at midnight, handled next day

            if (isActive && windowClosed)
            {
                int rows = await repo.TransitionStatusAsync(record.Id, record.Status, PrayerStatus.Missed);
                if (rows > 0)
                {
                    await repo.LogStatusHistoryAsync(new PrayerStatusHistory
                    {
                        Id              = Guid.NewGuid(),
                        PrayerRecordId  = record.Id,
                        OldStatus       = record.Status,
                        NewStatus       = PrayerStatus.Missed,
                        ChangedByUsername = "System",
                        ChangedAt       = DateTime.UtcNow
                    });

                    await push.SendToSubscriptionsByUserAsync(record.UserId,
                        $"Missed {record.PrayerName}",
                        $"{record.PrayerName} prayer time has ended. You can still make Qada.",
                        "/prayer");
                }
            }
        }

        private async Task HandleRemindersAsync(
            PrayerRecord record, TimeSpan localTime,
            List<PrayerReminderConfig> userReminders,
            PushNotificationService push, PrayerRepository prayerRepo)
        {
            foreach (var cfg in userReminders)
            {
                if (cfg.PrayerName != record.PrayerName && cfg.PrayerName != "All") continue;
                if (cfg.ReminderType == "EndOfDay") continue;

                var targetTime = record.PrayerTime + TimeSpan.FromMinutes(cfg.MinutesOffset);

                // Match by hour+minute to fire exactly once per minute
                bool timeMatches = localTime.Hours == targetTime.Hours
                                && localTime.Minutes == targetTime.Minutes;

                if (!timeMatches) continue;

                // Before-prayer reminders: only if still Upcoming/ReminderSent
                if (cfg.MinutesOffset < 0)
                {
                    if (record.Status is not (PrayerStatus.Upcoming or PrayerStatus.ReminderSent)) continue;

                    await push.SendToSubscriptionsByUserAsync(record.UserId,
                        $"{record.PrayerName} in {Math.Abs(cfg.MinutesOffset)} minutes",
                        $"{record.PrayerName} prayer starts at {FormatTime(record.PrayerTime)}. Get ready.",
                        "/prayer");

                    // Mark as ReminderSent if still Upcoming
                    if (record.Status == PrayerStatus.Upcoming)
                    {
                        await prayerRepo.TransitionStatusAsync(record.Id, PrayerStatus.Upcoming, PrayerStatus.ReminderSent);
                        record.Status = PrayerStatus.ReminderSent;
                    }
                }

                // At-prayer reminders: only if still Upcoming/ReminderSent
                if (cfg.MinutesOffset == 0 && cfg.ReminderType == "AtPrayer")
                {
                    if (record.Status is not (PrayerStatus.Upcoming or PrayerStatus.ReminderSent or PrayerStatus.Pending)) continue;

                    await push.SendToSubscriptionsByUserAsync(record.UserId,
                        $"It's time for {record.PrayerName}",
                        $"{record.PrayerName} prayer time has begun.",
                        "/prayer");
                }

                // After-prayer reminders: only if still Pending
                if (cfg.MinutesOffset > 0)
                {
                    if (record.Status != PrayerStatus.Pending) continue;

                    await push.SendToSubscriptionsByUserAsync(record.UserId,
                        $"Don't forget {record.PrayerName}",
                        $"You haven't marked {record.PrayerName} yet. Prayer time is running out.",
                        "/prayer");
                }
            }
        }

        private async Task SendEndOfDaySummaryAsync(
            Guid userId, Guid orgId, DateTime localDate,
            PrayerRepository repo, PushNotificationService push)
        {
            var records = (await repo.GetPendingTransitionRecordsAsync(orgId, localDate))
                .Where(r => r.UserId == userId)
                .ToList();

            var allRecords = await repo.GetRecordsByDateAsync(userId, orgId, localDate);
            int completed = allRecords.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
            int total     = 5;

            string body = completed == total
                ? "Excellent! You completed all 5 prayers today."
                : $"You completed {completed}/{total} prayers today. Don't miss the rest.";

            await push.SendToSubscriptionsByUserAsync(userId,
                $"Today's Prayer Summary: {completed}/{total}",
                body,
                "/prayer");
        }

        private static EffectivePrayerSettings ResolveSettings(PrayerOrgSettings org, PrayerUserSettings? user)
        {
            if (user?.Latitude is not null && user.Longitude is not null && user.Timezone is not null)
            {
                return new EffectivePrayerSettings
                {
                    Latitude          = user.Latitude.Value,
                    Longitude         = user.Longitude.Value,
                    Timezone          = user.Timezone,
                    CalculationMethod = org.CalculationMethod,
                    AsrMethod         = org.AsrMethod,
                    FajrAngle         = org.FajrAngle,
                    IshaAngle         = org.IshaAngle,
                    IsUserOverride    = true,
                    CityName          = user.CityName
                };
            }
            return new EffectivePrayerSettings
            {
                Latitude          = org.Latitude,
                Longitude         = org.Longitude,
                Timezone          = org.Timezone,
                CalculationMethod = org.CalculationMethod,
                AsrMethod         = org.AsrMethod,
                FajrAngle         = org.FajrAngle,
                IshaAngle         = org.IshaAngle,
                IsUserOverride    = false
            };
        }

        private static TimeZoneInfo SafeGetTz(string tz)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch { return TimeZoneInfo.Utc; }
        }

        private static string FormatTime(TimeSpan t)
            => DateTime.Today.Add(t).ToString("hh:mm tt");
    }
}
