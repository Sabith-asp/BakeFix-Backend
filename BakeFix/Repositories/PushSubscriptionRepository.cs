using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using Npgsql;

namespace BakeFix.Repositories
{
    public class PushSubscriptionRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;
        private readonly IAuditClock _clock;

        public PushSubscriptionRepository(IConfiguration config, ITenantContext tenant, IAuditClock clock)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
            _clock = clock;
        }

        public async Task SaveAsync(Models.PushSubscription sub)
        {
            using var connection = new NpgsqlConnection(_conn);
            sub.CreatedAt = AuditTimestamps.ForCreate(sub.CreatedAt, _clock);

            // Replace existing subscription for the same user+endpoint
            await connection.ExecuteAsync(
                "DELETE FROM PushSubscriptions WHERE UserId = @UserId AND Endpoint = @Endpoint",
                new { sub.UserId, sub.Endpoint });

            await connection.ExecuteAsync(
                @"INSERT INTO PushSubscriptions (Id, UserId, OrgId, Endpoint, P256dh, Auth, CreatedAt)
                  VALUES (@Id, @UserId, @OrgId, @Endpoint, @P256dh, @Auth, @CreatedAt)",
                sub);
        }

        public async Task DeleteAsync(string endpoint, Guid userId)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                "DELETE FROM PushSubscriptions WHERE Endpoint = @endpoint AND UserId = @userId",
                new { endpoint, userId });
        }

        public async Task DeleteStaleAsync(string endpoint)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                "DELETE FROM PushSubscriptions WHERE Endpoint = @endpoint",
                new { endpoint });
        }

        public async Task<IEnumerable<Models.PushSubscription>> GetByOrgIdAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<Models.PushSubscription>(
                "SELECT * FROM PushSubscriptions WHERE OrgId = @orgId",
                new { orgId });
        }

        public async Task<int> GetCountByOrgIdAsync(Guid orgId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PushSubscriptions WHERE OrgId = @orgId",
                new { orgId });
        }

        public async Task<IEnumerable<Models.PushSubscription>> GetByUserIdAsync(Guid userId)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<Models.PushSubscription>(
                "SELECT * FROM PushSubscriptions WHERE UserId = @userId",
                new { userId });
        }
    }
}
