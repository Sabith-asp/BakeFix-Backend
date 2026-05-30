using MySql.Data.MySqlClient;
using System.Diagnostics;

namespace BakeFix.Migrations
{
    public class DatabaseMigrator
    {
        private readonly string _connectionString;
        private readonly string _environment;
        private readonly bool _runMigrations;
        private readonly ILogger<DatabaseMigrator> _logger;

        private const string BootstrapSql = @"
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Id          INT AUTO_INCREMENT PRIMARY KEY,
                ScriptName  VARCHAR(255) NOT NULL UNIQUE,
                AppliedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                AppliedBy   VARCHAR(100) NOT NULL
            );";

        public DatabaseMigrator(IConfiguration config, IWebHostEnvironment env, ILogger<DatabaseMigrator> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
            _environment      = env.EnvironmentName;
            _logger           = logger;

            var raw = config["AppSettings:RunMigrations"] ?? "false";
            _runMigrations = raw.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task RunAsync()
        {
            if (!_runMigrations)
            {
                _logger.LogInformation("[Migrator] Skipped.");
                return;
            }

            _logger.LogInformation("[Migrator] Started.");

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await ExecuteNonQueryAsync(connection, BootstrapSql);

            var applied   = await GetAppliedScriptsAsync(connection);
            var scriptDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
            var scripts   = Directory.GetFiles(scriptDir, "*.sql")
                                     .OrderBy(f => Path.GetFileName(f))
                                     .ToList();

            var pending = scripts.Where(s => !applied.Contains(Path.GetFileName(s))).ToList();

            if (pending.Count == 0)
            {
                _logger.LogInformation("[Migrator] No pending migrations.");
                return;
            }

            var sw = Stopwatch.StartNew();
            int successCount = 0;

            foreach (var scriptPath in pending)
            {
                var scriptName = Path.GetFileName(scriptPath);
                var sql        = await File.ReadAllTextAsync(scriptPath);

                try
                {
                    await ExecuteNonQueryAsync(connection, sql);
                    await RecordAppliedAsync(connection, scriptName);
                    successCount++;
                    _logger.LogInformation("[Migrator] Applied: {Script}", scriptName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Migrator] Failed: {Script}", scriptName);
                    throw new Exception($"Migration failed at '{scriptName}'.", ex);
                }
            }

            _logger.LogInformation("[Migrator] Completed — {Count} migration(s) applied in {Ms}ms.",
                successCount, sw.ElapsedMilliseconds);
        }

        private static async Task<HashSet<string>> GetAppliedScriptsAsync(MySqlConnection connection)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd    = new MySqlCommand("SELECT ScriptName FROM SchemaVersions", connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(reader.GetString(0));
            return result;
        }

        private async Task RecordAppliedAsync(MySqlConnection connection, string scriptName)
        {
            const string query = "INSERT INTO SchemaVersions (ScriptName, AppliedBy) VALUES (@scriptName, @appliedBy)";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@scriptName", scriptName);
            cmd.Parameters.AddWithValue("@appliedBy",  _environment);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task ExecuteNonQueryAsync(MySqlConnection connection, string sql)
        {
            using var cmd = new MySqlCommand(sql, connection);
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
