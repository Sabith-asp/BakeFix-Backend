using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class NotificationSettingsRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public NotificationSettingsRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<NotificationSettings> GetAsync()
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var settings = await connection.QueryFirstOrDefaultAsync<NotificationSettings>(
                "SELECT * FROM NotificationSettings WHERE OrgId = @orgId",
                new { orgId });

            if (settings is null)
            {
                settings = new NotificationSettings { OrgId = orgId };
                await UpsertAsync(settings);
            }

            return settings;
        }

        public async Task<NotificationSettings?> GetByOrgIdAsync(Guid orgId)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<NotificationSettings>(
                "SELECT * FROM NotificationSettings WHERE OrgId = @orgId",
                new { orgId });
        }

        /// <summary>
        /// Returns settings for all orgs that have Notifications module enabled
        /// and have at least one reminder type turned on. Safe to call from a BackgroundService.
        /// </summary>
        public async Task<IEnumerable<NotificationSettings>> GetAllEnabledRemindersAsync()
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<NotificationSettings>(
                @"SELECT ns.* FROM NotificationSettings ns
                  INNER JOIN OrganizationModules om ON om.OrganizationId = ns.OrgId
                  INNER JOIN Modules m ON m.Id = om.ModuleId
                  WHERE m.Name = 'Notifications'
                    AND om.IsEnabled = TRUE
                    AND (ns.DailyReminderEnabled = TRUE OR ns.WeeklySummaryEnabled = TRUE)");
        }

        public async Task UpsertAsync(NotificationSettings settings)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO NotificationSettings (OrgId, DailyReminderEnabled, ReminderHour, WeeklySummaryEnabled, BudgetAlertsEnabled)
                  VALUES (@OrgId, @DailyReminderEnabled, @ReminderHour, @WeeklySummaryEnabled, @BudgetAlertsEnabled)
                  ON DUPLICATE KEY UPDATE
                    DailyReminderEnabled = @DailyReminderEnabled,
                    ReminderHour         = @ReminderHour,
                    WeeklySummaryEnabled = @WeeklySummaryEnabled,
                    BudgetAlertsEnabled  = @BudgetAlertsEnabled",
                settings);
        }
    }
}
