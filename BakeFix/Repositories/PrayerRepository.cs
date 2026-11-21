using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using Npgsql;

namespace BakeFix.Repositories
{
    public class PrayerRepository
    {
        private readonly string _conn;

        public PrayerRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        // ── Org Settings ─────────────────────────────────────────────────────

        public async Task<PrayerOrgSettings> GetOrgSettingsAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            var settings = await connection.QueryFirstOrDefaultAsync<PrayerOrgSettings>(
                "SELECT * FROM PrayerOrgSettings WHERE OrganizationId = @orgId",
                new { orgId });

            if (settings is not null) return settings;

            // Auto-create defaults on first access
            var defaults = new PrayerOrgSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId
            };
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerOrgSettings
                  (Id, OrganizationId, Latitude, Longitude, Timezone, CalculationMethod, AsrMethod, FajrAngle, IshaAngle)
                  VALUES (@Id, @OrganizationId, @Latitude, @Longitude, @Timezone, @CalculationMethod, @AsrMethod, @FajrAngle, @IshaAngle)
                  ON CONFLICT (OrganizationId) DO NOTHING",
                defaults);
            return defaults;
        }

        public async Task UpsertOrgSettingsAsync(PrayerOrgSettings s)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerOrgSettings
                  (Id, OrganizationId, Latitude, Longitude, Timezone, CalculationMethod, AsrMethod, FajrAngle, IshaAngle)
                  VALUES (@Id, @OrganizationId, @Latitude, @Longitude, @Timezone, @CalculationMethod, @AsrMethod, @FajrAngle, @IshaAngle)
                  ON CONFLICT (OrganizationId) DO UPDATE SET
                    Latitude = @Latitude, Longitude = @Longitude, Timezone = @Timezone,
                    CalculationMethod = @CalculationMethod, AsrMethod = @AsrMethod,
                    FajrAngle = @FajrAngle, IshaAngle = @IshaAngle",
                s);
        }

        // ── User Settings ────────────────────────────────────────────────────

        public async Task<PrayerUserSettings?> GetUserSettingsAsync(Guid userId, Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<PrayerUserSettings>(
                "SELECT * FROM PrayerUserSettings WHERE UserId = @userId AND OrganizationId = @orgId",
                new { userId, orgId });
        }

        public async Task UpsertUserSettingsAsync(PrayerUserSettings s)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerUserSettings (Id, OrganizationId, UserId, Latitude, Longitude, Timezone, CityName)
                  VALUES (@Id, @OrganizationId, @UserId, @Latitude, @Longitude, @Timezone, @CityName)
                  ON CONFLICT (OrganizationId, UserId) DO UPDATE SET
                    Latitude = @Latitude, Longitude = @Longitude,
                    Timezone = @Timezone, CityName = @CityName",
                s);
        }

        public async Task<IEnumerable<PrayerUserSettings>> GetAllUserSettingsByOrgAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerUserSettings>(
                "SELECT * FROM PrayerUserSettings WHERE OrganizationId = @orgId",
                new { orgId });
        }

        // ── Prayer Records ───────────────────────────────────────────────────

        public async Task EnsureTodayRecordsAsync(
            Guid userId, string username, Guid orgId,
            DateTime localDate, CalculatedPrayerTimes times)
        {
            using var connection = new NpgsqlConnection(_conn);
            foreach (var name in PrayerNames.All)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO PrayerRecords
                      (Id, OrganizationId, UserId, Username, PrayerName, PrayerDate, PrayerTime, PrayerEndTime, Status)
                      VALUES (@Id, @OrganizationId, @UserId, @Username, @PrayerName, @PrayerDate, @PrayerTime, @PrayerEndTime, 'Upcoming')
                      ON CONFLICT (OrganizationId, UserId, PrayerName, PrayerDate) DO UPDATE SET
                        PrayerTime    = CASE WHEN PrayerRecords.Status IN ('Upcoming','ReminderSent')
                                             THEN EXCLUDED.PrayerTime ELSE PrayerRecords.PrayerTime END,
                        PrayerEndTime = CASE WHEN PrayerRecords.Status IN ('Upcoming','ReminderSent')
                                             THEN EXCLUDED.PrayerEndTime ELSE PrayerRecords.PrayerEndTime END",
                    new
                    {
                        Id             = Guid.NewGuid(),
                        OrganizationId = orgId,
                        UserId         = userId,
                        Username       = username,
                        PrayerName     = name,
                        PrayerDate     = localDate.Date,
                        PrayerTime     = times.GetTime(name),
                        PrayerEndTime  = times.GetEndTime(name)
                    });
            }
        }

        public async Task<IEnumerable<PrayerRecord>> GetRecordsByDateAsync(Guid userId, Guid orgId, DateTime localDate)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerRecord>(
                @"SELECT * FROM PrayerRecords
                  WHERE OrganizationId = @orgId AND UserId = @userId AND PrayerDate = @localDate
                  ORDER BY PrayerTime ASC",
                new { orgId, userId, localDate = localDate.Date });
        }

        public async Task<IEnumerable<PrayerRecord>> GetRecordsByDateRangeAsync(
            Guid userId, Guid orgId, DateTime from, DateTime to)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerRecord>(
                @"SELECT * FROM PrayerRecords
                  WHERE OrganizationId = @orgId AND UserId = @userId
                    AND PrayerDate BETWEEN @from AND @to
                  ORDER BY PrayerDate DESC, PrayerTime ASC",
                new { orgId, userId, from = from.Date, to = to.Date });
        }

        public async Task<PrayerRecord?> GetRecordByIdAsync(Guid id, Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<PrayerRecord>(
                "SELECT * FROM PrayerRecords WHERE Id = @id AND OrganizationId = @orgId",
                new { id, orgId });
        }

        public async Task<bool> UpdateStatusAsync(
            Guid id, string status, DateTime? completionTime,
            Guid? updatedByUserId, string? updatedByUsername,
            string? notes, string? congregationType)
        {
            using var connection = new NpgsqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                @"UPDATE PrayerRecords
                  SET Status = @status,
                      ActualCompletionTime = @completionTime,
                      UpdatedByUserId      = @updatedByUserId,
                      UpdatedByUsername    = @updatedByUsername,
                      Notes                = COALESCE(@notes, Notes),
                      CongregationType     = COALESCE(@congregationType, CongregationType)
                  WHERE Id = @id",
                new { id, status, completionTime, updatedByUserId, updatedByUsername, notes, congregationType });
            return rows > 0;
        }

        // Used by scheduler — no tenant context needed
        public async Task<int> TransitionStatusAsync(Guid id, string expectedStatus, string newStatus)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.ExecuteAsync(
                "UPDATE PrayerRecords SET Status = @newStatus WHERE Id = @id AND Status = @expectedStatus",
                new { id, expectedStatus, newStatus });
        }

        public async Task LogStatusHistoryAsync(PrayerStatusHistory history)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerStatusHistory
                  (Id, PrayerRecordId, OldStatus, NewStatus, ChangedByUserId, ChangedByUsername, Note, ChangedAt)
                  VALUES (@Id, @PrayerRecordId, @OldStatus, @NewStatus, @ChangedByUserId, @ChangedByUsername, @Note, @ChangedAt)",
                history);
        }

        public async Task<IEnumerable<PrayerStatusHistory>> GetHistoryByRecordAsync(Guid recordId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerStatusHistory>(
                "SELECT * FROM PrayerStatusHistory WHERE PrayerRecordId = @recordId ORDER BY ChangedAt ASC",
                new { recordId });
        }

        // ── Reminder Configs ─────────────────────────────────────────────────

        public async Task<IEnumerable<PrayerReminderConfig>> GetReminderConfigsAsync(Guid userId, Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerReminderConfig>(
                "SELECT * FROM PrayerReminderConfigs WHERE UserId = @userId AND OrganizationId = @orgId ORDER BY PrayerName, MinutesOffset",
                new { userId, orgId });
        }

        public async Task UpsertReminderConfigAsync(PrayerReminderConfig cfg)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerReminderConfigs
                  (Id, OrganizationId, UserId, PrayerName, ReminderType, MinutesOffset, IsEnabled)
                  VALUES (@Id, @OrganizationId, @UserId, @PrayerName, @ReminderType, @MinutesOffset, @IsEnabled)
                  ON CONFLICT (UserId, PrayerName, ReminderType) DO UPDATE SET IsEnabled = @IsEnabled",
                cfg);
        }

        public async Task EnsureDefaultRemindersAsync(Guid userId, Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            int count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PrayerReminderConfigs WHERE UserId = @userId AND OrganizationId = @orgId",
                new { userId, orgId });

            if (count > 0) return;

            var defaults = BuildDefaultReminders(userId, orgId);
            foreach (var cfg in defaults)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO PrayerReminderConfigs
                      (Id, OrganizationId, UserId, PrayerName, ReminderType, MinutesOffset, IsEnabled)
                      VALUES (@Id, @OrganizationId, @UserId, @PrayerName, @ReminderType, @MinutesOffset, @IsEnabled)
                      ON CONFLICT (UserId, PrayerName, ReminderType) DO NOTHING",
                    cfg);
            }
        }

        private static List<PrayerReminderConfig> BuildDefaultReminders(Guid userId, Guid orgId)
        {
            var configs = new List<PrayerReminderConfig>();
            var types = new[]
            {
                ("BeforePrayer_15", -15),
                ("AtPrayer",         0),
                ("AfterPrayer_30",  30),
            };

            foreach (var prayer in PrayerNames.All)
            foreach (var (rtype, offset) in types)
            {
                configs.Add(new PrayerReminderConfig
                {
                    Id             = Guid.NewGuid(),
                    OrganizationId = orgId,
                    UserId         = userId,
                    PrayerName     = prayer,
                    ReminderType   = rtype,
                    MinutesOffset  = offset,
                    IsEnabled      = true
                });
            }

            // End-of-day summary (once per day, prayer-agnostic — stored under "All")
            configs.Add(new PrayerReminderConfig
            {
                Id             = Guid.NewGuid(),
                OrganizationId = orgId,
                UserId         = userId,
                PrayerName     = "All",
                ReminderType   = "EndOfDay",
                MinutesOffset  = 0,
                IsEnabled      = true
            });

            return configs;
        }

        // All enabled reminder configs across org (used by scheduler)
        public async Task<IEnumerable<PrayerReminderConfig>> GetEnabledRemindersByOrgAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerReminderConfig>(
                "SELECT * FROM PrayerReminderConfigs WHERE OrganizationId = @orgId AND IsEnabled = TRUE",
                new { orgId });
        }

        // ── Streaks ──────────────────────────────────────────────────────────

        public async Task<PrayerStreak?> GetStreakAsync(Guid userId, Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<PrayerStreak>(
                "SELECT * FROM PrayerStreaks WHERE UserId = @userId AND OrganizationId = @orgId",
                new { userId, orgId });
        }

        public async Task UpsertStreakAsync(PrayerStreak streak)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO PrayerStreaks
                  (Id, OrganizationId, UserId, CurrentStreak, LongestStreak, LastStreakDate,
                   TotalPrayersCompleted, TotalPrayersOnTime)
                  VALUES (@Id, @OrganizationId, @UserId, @CurrentStreak, @LongestStreak,
                          @LastStreakDate, @TotalPrayersCompleted, @TotalPrayersOnTime)
                  ON CONFLICT (OrganizationId, UserId) DO UPDATE SET
                    CurrentStreak         = @CurrentStreak,
                    LongestStreak         = @LongestStreak,
                    LastStreakDate         = @LastStreakDate,
                    TotalPrayersCompleted  = @TotalPrayersCompleted,
                    TotalPrayersOnTime     = @TotalPrayersOnTime",
                streak);
        }

        // ── Scheduler helpers ────────────────────────────────────────────────

        public async Task<IEnumerable<Guid>> GetOrgsWithPrayerModuleAsync()
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<Guid>(
                @"SELECT om.OrganizationId
                  FROM OrganizationModules om
                  JOIN Modules m ON m.Id = om.ModuleId
                  WHERE m.Name = 'Prayer' AND om.IsEnabled = TRUE");
        }

        public async Task<IEnumerable<(Guid Id, string Username)>> GetUsersByOrgAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            // dynamic rows bypass Dapper's registered type handlers, so map
            // straight to a value tuple instead (Dapper supports this
            // positionally) rather than going through `dynamic`.
            return await connection.QueryAsync<(Guid Id, string Username)>(
                "SELECT Id, Username FROM UsersOfBakeFix WHERE OrganizationId = @orgId",
                new { orgId });
        }

        public async Task<IEnumerable<PrayerRecord>> GetPendingTransitionRecordsAsync(Guid orgId, DateTime localDate)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerRecord>(
                @"SELECT * FROM PrayerRecords
                  WHERE OrganizationId = @orgId
                    AND PrayerDate = @localDate
                    AND Status IN ('Upcoming', 'ReminderSent', 'Pending')",
                new { orgId, localDate = localDate.Date });
        }

        // ── Admin ────────────────────────────────────────────────────────────

        public async Task<IEnumerable<PrayerRecord>> GetOrgRecordsByDateAsync(Guid orgId, DateTime localDate)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerRecord>(
                @"SELECT * FROM PrayerRecords
                  WHERE OrganizationId = @orgId AND PrayerDate = @localDate
                  ORDER BY UserId, PrayerTime ASC",
                new { orgId, localDate = localDate.Date });
        }

        public async Task<IEnumerable<PrayerStreak>> GetAllStreaksByOrgAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<PrayerStreak>(
                @"SELECT * FROM PrayerStreaks WHERE OrganizationId = @orgId
                  ORDER BY CurrentStreak DESC",
                new { orgId });
        }
    }
}
