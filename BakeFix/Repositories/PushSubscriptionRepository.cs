using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class PushSubscriptionRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public PushSubscriptionRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task SaveAsync(Models.PushSubscription sub)
        {
            using var connection = new MySqlConnection(_conn);
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
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                "DELETE FROM PushSubscriptions WHERE Endpoint = @endpoint AND UserId = @userId",
                new { endpoint, userId });
        }

        public async Task DeleteStaleAsync(string endpoint)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                "DELETE FROM PushSubscriptions WHERE Endpoint = @endpoint",
                new { endpoint });
        }

        public async Task<IEnumerable<Models.PushSubscription>> GetByOrgIdAsync(Guid orgId)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<Models.PushSubscription>(
                "SELECT * FROM PushSubscriptions WHERE OrgId = @orgId",
                new { orgId });
        }

        public async Task<int> GetCountByOrgIdAsync(Guid orgId)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PushSubscriptions WHERE OrgId = @orgId",
                new { orgId });
        }
    }
}
