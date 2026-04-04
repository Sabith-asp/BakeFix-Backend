using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class NotificationSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<NotificationSchedulerService> _logger;

        public NotificationSchedulerService(IServiceProvider services, ILogger<NotificationSchedulerService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int lastProcessedHour = -1;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                if (now.Hour != lastProcessedHour)
                {
                    lastProcessedHour = now.Hour;
                    await ProcessHourAsync(now);
                }

                // Check again in 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessHourAsync(DateTime now)
        {
            try
            {
                using var scope = _services.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<NotificationSettingsRepository>();
                var pushService  = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

                var allSettings = await settingsRepo.GetAllEnabledRemindersAsync();

                foreach (var settings in allSettings)
                {
                    // Daily reminder: when UTC hour matches the org's configured hour
                    if (settings.DailyReminderEnabled && now.Hour == settings.ReminderHour)
                    {
                        await pushService.SendToOrgAsync(
                            settings.OrgId,
                            "Fynlo Reminder",
                            "Don't forget to log today's transactions 📋",
                            "/expenses");

                        _logger.LogInformation("Sent daily reminder to org {OrgId}", settings.OrgId);
                    }

                    // Weekly summary: Monday at 9 AM UTC
                    if (settings.WeeklySummaryEnabled && now.DayOfWeek == DayOfWeek.Monday && now.Hour == 9)
                    {
                        await pushService.SendToOrgAsync(
                            settings.OrgId,
                            "Fynlo Weekly Summary",
                            "Your weekly financial summary is ready 📊",
                            "/");

                        _logger.LogInformation("Sent weekly summary to org {OrgId}", settings.OrgId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification scheduler at hour {Hour}", now.Hour);
            }
        }
    }
}
