using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class WageRepository
    {
        private readonly string _conn;

        public WageRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Wage>> GetAllAsync(DateTime? startDate, DateTime? endDate)
        {
            using var connection = new MySqlConnection(_conn);

            string query = @"SELECT * FROM Wages
                             WHERE (@startDate IS NULL OR `Date` >= @startDate)
                               AND (@endDate IS NULL OR `Date` <= @endDate)
                             ORDER BY CreatedAt DESC";

            return await connection.QueryAsync<Wage>(query, new { startDate, endDate });
        }

        public async Task<Wage> CreateAsync(Wage wage)
        {
            using var connection = new MySqlConnection(_conn);

            string query = @"INSERT INTO Wages 
                            (Id, Amount, EmployeeName, Description, `Date`, CreatedAt)
                            VALUES (@Id, @Amount, @EmployeeName, @Description, @Date, @CreatedAt);";

            await connection.ExecuteAsync(query, wage);

            return wage;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            string query = "DELETE FROM Wages WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }
}