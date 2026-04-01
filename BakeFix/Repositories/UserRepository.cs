using BakeFix.DTOs;
using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IEnumerable<OrgUserResponse>> GetUsersByOrgAsync(Guid orgId)
        {
            const string query = @"
                SELECT u.Id, u.Username, r.Name AS Role
                FROM UsersOfBakeFix u
                JOIN Roles r ON r.Id = u.RoleId
                WHERE u.OrganizationId = @orgId
                ORDER BY u.Username ASC";

            using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<OrgUserResponse>(query, new { orgId });
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            const string query = "SELECT COUNT(1) FROM UsersOfBakeFix WHERE Username = @username";
            using var connection = new MySqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(query, new { username }) > 0;
        }

        public async Task CreateUserAsync(User user)
        {
            const string query = @"
                INSERT INTO UsersOfBakeFix (Id, Username, Password, PasswordHash, OrganizationId, RoleId)
                VALUES (@Id, @Username, @Password, @PasswordHash, @OrganizationId, @RoleId)";

            using var connection = new MySqlConnection(_connectionString);
            await connection.ExecuteAsync(query, user);
        }

        public async Task DeleteUserAsync(Guid userId)
        {
            const string query = "DELETE FROM UsersOfBakeFix WHERE Id = @userId";
            using var connection = new MySqlConnection(_connectionString);
            await connection.ExecuteAsync(query, new { userId });
        }

        /// <summary>
        /// Called on first login for legacy users whose PasswordHash contains
        /// plain text. Writes the original plain text to Password and the
        /// bcrypt hash to PasswordHash, bringing the row up to the new schema.
        /// </summary>
        public async Task MigratePasswordAsync(Guid userId, string plainPassword, string bcryptHash)
        {
            const string query = @"UPDATE UsersOfBakeFix
                                   SET Password = @plainPassword, PasswordHash = @bcryptHash
                                   WHERE Id = @userId";
            using var connection = new MySqlConnection(_connectionString);
            await connection.ExecuteAsync(query, new { userId, plainPassword, bcryptHash });
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            const string query = @"
                SELECT u.Id,
                       u.Username,
                       u.Password,
                       u.PasswordHash,
                       u.OrganizationId,
                       u.RoleId,
                       r.Name        AS Role,
                       o.Name        AS OrganizationName,
                       o.IsActive    AS OrgIsActive
                FROM UsersOfBakeFix u
                LEFT JOIN Roles r ON r.Id = u.RoleId
                LEFT JOIN Organizations o ON o.Id = u.OrganizationId
                WHERE u.Username = @Username";

            using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<User>(query, new { Username = username });
        }
    }
}
