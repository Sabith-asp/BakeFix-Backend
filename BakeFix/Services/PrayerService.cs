using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class PrayerService
    {
        private readonly PrayerRepository _repo;
        private readonly ITenantContext _tenant;
        private readonly ILogger<PrayerService> _logger;

        public PrayerService(PrayerRepository repo, ITenantContext tenant, ILogger<PrayerService> logger)
        {
            _repo   = repo;
            _tenant = tenant;
            _logger = logger;
        }

        // ── Effective settings: user override → org default ──────────────────

        public async Task<EffectivePrayerSettings> GetEffectiveSettingsAsync()
        {
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            var org  = await _repo.GetOrgSettingsAsync(orgId);
            var user = await _repo.GetUserSettingsAsync(userId, orgId);

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

        // ── Ensure today's records exist ─────────────────────────────────────

        private async Task<List<PrayerRecord>> EnsureAndGetTodayAsync()
        {
            var orgId    = _tenant.RequiredOrgId;
            var userId   = _tenant.RequiredUserId;
            var username = _tenant.Username;
            var settings = await GetEffectiveSettingsAsync();

            TimeZoneInfo tzi;
            try { tzi = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone); }
            catch { tzi = TimeZoneInfo.Utc; }

            var localNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
            var localDate = localNow.Date;
            var prayers   = PrayerTimeCalculator.Calculate(settings, localDate);

            await _repo.EnsureTodayRecordsAsync(userId, username, orgId, localDate, prayers);
            var records = (await _repo.GetRecordsByDateAsync(userId, orgId, localDate)).ToList();
            return records;
        }

        // ── Dashboard ────────────────────────────────────────────────────────

        public async Task<PrayerDashboardResponse> GetDashboardAsync()
        {
            await _repo.EnsureDefaultRemindersAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);

            var records  = await EnsureAndGetTodayAsync();
            var settings = await GetEffectiveSettingsAsync();
            var orgSettings = await _repo.GetOrgSettingsAsync(_tenant.RequiredOrgId);
            var streak   = await _repo.GetStreakAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);

            TimeZoneInfo tzi;
            try { tzi = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone); }
            catch { tzi = TimeZoneInfo.Utc; }

            var localNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
            var localDate = localNow.Date;
            var prayers   = PrayerTimeCalculator.Calculate(settings, localDate);
            var localTime = localNow.TimeOfDay;

            var completed = records.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
            var pending   = records.Count(r => r.Status == PrayerStatus.Pending);
            var missed    = records.Count(r => r.Status == PrayerStatus.Missed);
            var excused   = records.Count(r => r.Status == PrayerStatus.Excused);

            var next        = prayers.GetNextPrayer(localTime);
            var nextName    = next?.Name;
            var nextTime    = next.HasValue ? FormatTime(next.Value.Time) : null;
            var minutesToNext = next.HasValue
                ? (int)(next.Value.Time - localTime).TotalMinutes
                : (int?)null;

            // Always overlay fresh calculated times so records reflect the current location,
            // even when a prayer was created with stale coordinates (e.g., Mecca defaults).
            foreach (var r in records)
            {
                try
                {
                    r.PrayerTime    = prayers.GetTime(r.PrayerName);
                    r.PrayerEndTime = prayers.GetEndTime(r.PrayerName);
                }
                catch { }
            }

            return new PrayerDashboardResponse
            {
                TodayDate          = localDate.ToString("yyyy-MM-dd"),
                TodayDay           = localDate.ToString("dddd, d MMMM yyyy"),
                CompletedCount     = completed,
                PendingCount       = pending,
                MissedCount        = missed,
                ExcusedCount       = excused,
                CompletionPercentage = Math.Round(completed / 5.0 * 100, 1),
                CurrentPrayer      = prayers.GetCurrentPrayer(localTime),
                NextPrayer         = nextName,
                NextPrayerTime     = nextTime,
                MinutesToNextPrayer = minutesToNext,
                Streak             = streak,
                Prayers            = records,
                OrgSettings        = orgSettings
            };
        }

        // ── History ──────────────────────────────────────────────────────────

        public async Task<List<PrayerHistoryDay>> GetHistoryAsync(DateTime from, DateTime to)
        {
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            var records = await _repo.GetRecordsByDateRangeAsync(userId, orgId, from, to);

            return records
                .GroupBy(r => r.PrayerDate.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new PrayerHistoryDay
                {
                    Date           = g.Key.ToString("yyyy-MM-dd"),
                    CompletedCount = g.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status)),
                    TotalCount     = 5,
                    Prayers        = g.OrderBy(r => r.PrayerTime).ToList()
                })
                .ToList();
        }

        // ── Status Update ────────────────────────────────────────────────────

        public async Task<PrayerRecord> UpdateStatusAsync(Guid recordId, UpdatePrayerStatusRequest request)
        {
            if (!PrayerStatus.ValidUserSetStatuses.Contains(request.Status))
                throw new ArgumentException($"Invalid status: {request.Status}");

            var orgId    = _tenant.RequiredOrgId;
            var userId   = _tenant.RequiredUserId;
            var username = _tenant.Username;

            var record = await _repo.GetRecordByIdAsync(recordId, orgId)
                ?? throw new ArgumentException("Prayer record not found.");

            if (record.UserId != userId && _tenant.Role != "Admin")
                throw new UnauthorizedAccessException("You can only update your own prayer records.");

            DateTime? completionTime = PrayerStatus.CompletedStatuses.Contains(request.Status)
                ? DateTime.UtcNow
                : null;

            var oldStatus = record.Status;
            await _repo.UpdateStatusAsync(
                recordId, request.Status, completionTime,
                userId, username, request.Notes, request.CongregationType);

            await _repo.LogStatusHistoryAsync(new PrayerStatusHistory
            {
                Id              = Guid.NewGuid(),
                PrayerRecordId  = recordId,
                OldStatus       = oldStatus,
                NewStatus       = request.Status,
                ChangedByUserId = userId,
                ChangedByUsername = username,
                Note            = request.Notes,
                ChangedAt       = DateTime.UtcNow
            });

            try
            {
                await UpdateStreakAsync(userId, orgId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrayerService] UpdateStreak failed for user {UserId} org {OrgId}", userId, orgId);
            }

            return (await _repo.GetRecordByIdAsync(recordId, orgId))!;
        }

        // ── Streak calculation ───────────────────────────────────────────────

        private async Task UpdateStreakAsync(Guid userId, Guid orgId)
        {
            var streak = await _repo.GetStreakAsync(userId, orgId) ?? new PrayerStreak
            {
                Id             = Guid.NewGuid(),
                OrganizationId = orgId,
                UserId         = userId
            };

            // Count totals from all records
            var allRecords = await _repo.GetRecordsByDateRangeAsync(
                userId, orgId,
                DateTime.UtcNow.Date.AddDays(-365),
                DateTime.UtcNow.Date);

            streak.TotalPrayersCompleted = allRecords.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
            streak.TotalPrayersOnTime    = allRecords.Count(r => r.Status == PrayerStatus.CompletedOnTime);

            // Calculate current streak (consecutive complete days)
            var byDay = allRecords
                .GroupBy(r => r.PrayerDate.Date)
                .OrderByDescending(g => g.Key)
                .ToList();

            int current = 0;
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            foreach (var day in byDay)
            {
                if (day.Key > DateTime.UtcNow.Date) continue;

                bool dayComplete = day.All(r =>
                    PrayerStatus.CompletedStatuses.Contains(r.Status) ||
                    r.Status == PrayerStatus.Excused);

                if (!dayComplete) break;

                if (current == 0 && day.Key < yesterday.AddDays(-1)) break;
                current++;
            }

            streak.CurrentStreak = current;
            streak.LongestStreak = Math.Max(streak.LongestStreak, current);
            if (current > 0) streak.LastStreakDate = DateTime.UtcNow.Date;

            await _repo.UpsertStreakAsync(streak);
        }

        // ── Reminder configs ─────────────────────────────────────────────────

        public async Task<IEnumerable<PrayerReminderConfig>> GetReminderConfigsAsync()
        {
            await _repo.EnsureDefaultRemindersAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);
            return await _repo.GetReminderConfigsAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);
        }

        public async Task UpdateReminderConfigAsync(UpdateReminderConfigRequest request)
        {
            var validPrayers = PrayerNames.All.Append("All").ToArray();
            if (!validPrayers.Contains(request.PrayerName))
                throw new ArgumentException("Invalid prayer name.");

            await _repo.UpsertReminderConfigAsync(new PrayerReminderConfig
            {
                Id             = Guid.NewGuid(),
                OrganizationId = _tenant.RequiredOrgId,
                UserId         = _tenant.RequiredUserId,
                PrayerName     = request.PrayerName,
                ReminderType   = request.ReminderType,
                MinutesOffset  = request.MinutesOffset,
                IsEnabled      = request.IsEnabled
            });
        }

        // ── Org Settings ─────────────────────────────────────────────────────

        public async Task<PrayerOrgSettings> GetOrgSettingsAsync()
            => await _repo.GetOrgSettingsAsync(_tenant.RequiredOrgId);

        public async Task UpdateOrgSettingsAsync(UpdateOrgSettingsRequest request)
        {
            if (_tenant.Role != "Admin" && !_tenant.IsSuperAdmin)
                throw new UnauthorizedAccessException("Only administrators can change organization prayer settings.");

            var validMethods = new[] { "MWL", "ISNA", "Egyptian", "Karachi", "UmmAlQura", "Custom" };
            if (!validMethods.Contains(request.CalculationMethod))
                throw new ArgumentException("Invalid calculation method.");

            if (string.IsNullOrWhiteSpace(request.Timezone))
                throw new ArgumentException("Timezone is required.");

            var existing = await _repo.GetOrgSettingsAsync(_tenant.RequiredOrgId);
            existing.Latitude          = request.Latitude;
            existing.Longitude         = request.Longitude;
            existing.Timezone          = request.Timezone;
            existing.CalculationMethod = request.CalculationMethod;
            existing.AsrMethod         = request.AsrMethod;
            existing.FajrAngle         = request.FajrAngle;
            existing.IshaAngle         = request.IshaAngle;

            await _repo.UpsertOrgSettingsAsync(existing);
        }

        // ── User Location ────────────────────────────────────────────────────

        public async Task<PrayerUserSettings?> GetUserSettingsAsync()
            => await _repo.GetUserSettingsAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);

        public async Task UpdateUserLocationAsync(UpdateUserLocationRequest request)
        {
            var existing = await _repo.GetUserSettingsAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId)
                ?? new PrayerUserSettings
                {
                    Id             = Guid.NewGuid(),
                    OrganizationId = _tenant.RequiredOrgId,
                    UserId         = _tenant.RequiredUserId
                };

            existing.Latitude  = request.Latitude;
            existing.Longitude = request.Longitude;
            existing.Timezone  = request.Timezone;
            existing.CityName  = request.CityName;

            await _repo.UpsertUserSettingsAsync(existing);
        }

        public async Task ClearUserLocationAsync()
        {
            var s = await _repo.GetUserSettingsAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);
            if (s is null) return;

            s.Latitude  = null;
            s.Longitude = null;
            s.Timezone  = null;
            s.CityName  = null;
            await _repo.UpsertUserSettingsAsync(s);
        }

        // ── Streak ───────────────────────────────────────────────────────────

        public async Task<PrayerStreak?> GetStreakAsync()
            => await _repo.GetStreakAsync(_tenant.RequiredUserId, _tenant.RequiredOrgId);

        // ── Admin summary ────────────────────────────────────────────────────

        public async Task<PrayerAdminSummary> GetAdminSummaryAsync(DateTime? date)
        {
            var orgId     = _tenant.RequiredOrgId;
            var localDate = date ?? DateTime.UtcNow.Date;

            var records = (await _repo.GetOrgRecordsByDateAsync(orgId, localDate)).ToList();
            var streaks = (await _repo.GetAllStreaksByOrgAsync(orgId)).ToList();
            var streakMap = streaks.ToDictionary(s => s.UserId);

            var userGroups = records.GroupBy(r => r.UserId).ToList();
            int totalUsers = userGroups.Count;

            var userStats = userGroups.Select(g =>
            {
                int comp = g.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
                streakMap.TryGetValue(g.Key, out var streak);
                return new PrayerUserStat
                {
                    UserId              = g.Key.ToString(),
                    Username            = g.First().Username,
                    CompletedToday      = comp,
                    CompletionRate      = (int)Math.Round(comp / 5.0 * 100),
                    CurrentStreak       = streak?.CurrentStreak ?? 0,
                    LongestStreak       = streak?.LongestStreak ?? 0,
                    TotalPrayersCompleted = streak?.TotalPrayersCompleted ?? 0
                };
            }).OrderByDescending(u => u.CompletedToday).ToList();

            var prayerStats = PrayerNames.All.Select(name =>
            {
                var grp = records.Where(r => r.PrayerName == name).ToList();
                int comp = grp.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
                int miss = grp.Count(r => r.Status == PrayerStatus.Missed);
                int pend = grp.Count(r => r.Status == PrayerStatus.Pending);
                return new PrayerNameStat
                {
                    PrayerName     = name,
                    CompletedCount = comp,
                    MissedCount    = miss,
                    PendingCount   = pend,
                    CompletionRate = grp.Count > 0 ? Math.Round(comp / (double)grp.Count * 100, 1) : 0
                };
            }).ToList();

            int totalComp  = records.Count(r => PrayerStatus.CompletedStatuses.Contains(r.Status));
            int totalMiss  = records.Count(r => r.Status == PrayerStatus.Missed);
            int totalPend  = records.Count(r => r.Status == PrayerStatus.Pending);
            int totalPoss  = totalUsers * 5;

            return new PrayerAdminSummary
            {
                Date               = localDate.ToString("yyyy-MM-dd"),
                TotalUsers         = totalUsers,
                TotalPossiblePrayers = totalPoss,
                TotalCompleted     = totalComp,
                TotalMissed        = totalMiss,
                TotalPending       = totalPend,
                OrgCompletionRate  = totalPoss > 0 ? Math.Round(totalComp / (double)totalPoss * 100, 1) : 0,
                UserStats          = userStats,
                PrayerStats        = prayerStats
            };
        }

        private static string FormatTime(TimeSpan t)
        {
            var dt = DateTime.Today.Add(t);
            return dt.ToString("hh:mm tt");
        }
    }
}
