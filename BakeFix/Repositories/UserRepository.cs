using BakeFix.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BakeFix.Repositories
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            const string query = "SELECT Id, Username, PasswordHash FROM Users WHERE Username = @Username";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<User>(query, new { Username = username });
        }
    }
}
