using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class TaskCarryForwardService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TaskCarryForwardService> _logger;

        public TaskCarryForwardService(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            TimeProvider timeProvider,
            ILogger<TaskCarryForwardService> logger)
        {
            _scopeFactory = scopeFactory;
            _config       = config;
            _timeProvider = timeProvider;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Catch up immediately on startup — otherwise a server restart shortly after
            // a missed local midnight leaves already-overdue tasks stuck until the next
            // scheduled run.
            try
            {
                await RunCarryForwardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CarryForward] Error during startup catch-up run.");
            }

            var pollInterval = TimeSpan.FromMinutes(_config.GetValue<double?>("CarryForward:PollIntervalMinutes") ?? 15);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(pollInterval, stoppingToken);
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await RunCarryForwardAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CarryForward] Error during carry-forward.");
                }
            }
        }

        // Each org gets its own locally-resolved "today" — a single global cutoff (the old
        // BusinessTime.Today, hardcoded to Asia/Kolkata) would carry forward tasks at the
        // wrong moment for every org on a different timezone.
        private async Task RunCarryForwardAsync()
        {
            var connString = _config.GetConnectionString("DefaultConnection")!;
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            using var scope = _scopeFactory.CreateScope();
            var orgRepo = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var repo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();

            var orgs = await orgRepo.GetAllAsync();

            int totalCarried = 0;
            foreach (var org in orgs)
            {
                var today = OrgTimeConversion.LocalDate(utcNow, org.Timezone);

                var overdue = (await repo.GetOverdueForOrgAsync(connString, org.Id, today)).ToList();
                if (overdue.Count == 0) continue;

                var ids  = overdue.Select(t => t.Id).ToList();
                var logs = overdue.Select(t => new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = t.Id,
                    PerformedByUserId   = t.CreatedByUserId,
                    PerformedByUsername = "System",
                    ActivityType        = "CarryForward",
                    OldValue            = t.CurrentTargetDate.ToString("yyyy-MM-dd"),
                    NewValue            = today.ToString("yyyy-MM-dd"),
                    CreatedAt           = utcNow
                }).ToList();

                await repo.BulkCarryForwardAsync(connString, ids, today);
                await repo.BulkLogActivityAsync(connString, logs);

                totalCarried += overdue.Count;
                _logger.LogInformation(
                    "[CarryForward] Carried forward {Count} task(s) for org {OrgId} to {Date} ({Timezone}).",
                    overdue.Count, org.Id, today.ToString("yyyy-MM-dd"), org.Timezone);
            }

            if (totalCarried == 0)
                _logger.LogInformation("[CarryForward] No overdue tasks across any org.");
        }
    }
}
