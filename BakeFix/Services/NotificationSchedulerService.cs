using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class NotificationSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<NotificationSchedulerService> _logger;

        // Tracks the last org-local date a given org already received a given reminder
        // kind on, so each org dedupes against its own local day instead of a single
        // global "processed this UTC hour" gate (which silently assumed every org's
        // reminder hour landed on a UTC hour boundary — wrong for half-hour-offset
        // zones like Asia/Kolkata).
        private readonly Dictionary<(Guid OrgId, string Kind), DateTime> _lastSentLocalDate = new();

        public NotificationSchedulerService(IServiceProvider services, TimeProvider timeProvider, ILogger<NotificationSchedulerService> logger)
        {
            _services = services;
            _timeProvider = timeProvider;
            _logger = logger;
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
                using var scope = _services.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<NotificationSettingsRepository>();
                var pushService  = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

                var allSettings = await settingsRepo.GetAllEnabledRemindersAsync();
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var settings in allSettings)
                {
                    var localNow = OrgTimeConversion.ToLocal(utcNow, settings.Timezone);

                    // Daily reminder: when the org's own local hour matches its configured hour.
                    if (settings.DailyReminderEnabled
                        && localNow.Hour == settings.ReminderHour
                        && _lastSentLocalDate.GetValueOrDefault((settings.OrgId, "Daily")) != localNow.Date)
                    {
                        await pushService.SendToOrgAsync(
                            settings.OrgId,
                            "Fynlo Reminder",
                            "Don't forget to log today's transactions 📋",
                            "/expenses");

                        _lastSentLocalDate[(settings.OrgId, "Daily")] = localNow.Date;
                        _logger.LogInformation("Sent daily reminder to org {OrgId}", settings.OrgId);
                    }

                    // Weekly summary: Monday at 9 AM in the org's own local time.
                    if (settings.WeeklySummaryEnabled
                        && localNow.DayOfWeek == DayOfWeek.Monday
                        && localNow.Hour == 9
                        && _lastSentLocalDate.GetValueOrDefault((settings.OrgId, "Weekly")) != localNow.Date)
                    {
                        await pushService.SendToOrgAsync(
                            settings.OrgId,
                            "Fynlo Weekly Summary",
                            "Your weekly financial summary is ready 📊",
                            "/");

                        _lastSentLocalDate[(settings.OrgId, "Weekly")] = localNow.Date;
                        _logger.LogInformation("Sent weekly summary to org {OrgId}", settings.OrgId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification scheduler");
            }
        }
    }
}
