using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class TaskCarryForwardService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<TaskCarryForwardService> _logger;

        public TaskCarryForwardService(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<TaskCarryForwardService> logger)
        {
            _scopeFactory = scopeFactory;
            _config       = config;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now         = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1);
                var delay        = nextMidnight - now;

                _logger.LogInformation("[CarryForward] Next run in {Minutes} minutes.", (int)delay.TotalMinutes);
                await Task.Delay(delay, stoppingToken);

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

        private async Task RunCarryForwardAsync()
        {
            var connString = _config.GetConnectionString("DefaultConnection")!;
            var today      = DateTime.UtcNow.Date;

            // Use a plain repo instance without tenant context for cross-org operation
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

            var overdue = (await repo.GetOverdueAllOrgsAsync(connString)).ToList();
            if (overdue.Count == 0)
            {
                _logger.LogInformation("[CarryForward] No overdue tasks.");
                return;
            }

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
                CreatedAt           = DateTime.UtcNow
            }).ToList();

            await repo.BulkCarryForwardAsync(connString, ids, today);
            await repo.BulkLogActivityAsync(connString, logs);

            _logger.LogInformation("[CarryForward] Carried forward {Count} tasks to {Date}.", overdue.Count, today.ToString("yyyy-MM-dd"));
        }
    }
}
