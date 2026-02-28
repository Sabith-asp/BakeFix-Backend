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

        public async Task<IEnumerable<Wage>> GetAllAsync(DateTime? startDate, DateTime? endDate, int? limit)
        {
            using var connection = new MySqlConnection(_conn);

            string query = @"SELECT w.Id,
                                    w.Amount,
                                    w.EmployeeId,
                                    e.Name AS EmployeeName,
                                    w.Description,
                                    w.`Date`,
                                    w.CreatedAt
                             FROM Wages w
                             LEFT JOIN Employees e ON e.Id = w.EmployeeId
                             WHERE (@startDate IS NULL OR w.`Date` >= @startDate)
                               AND (@endDate IS NULL OR w.`Date` <= @endDate)
                             ORDER BY w.CreatedAt DESC";

            if (limit.HasValue)
            {
                query += " LIMIT @limit";
            }

            return await connection.QueryAsync<Wage>(query, new { startDate, endDate, limit });
        }

        public async Task<Wage> CreateAsync(Wage wage)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"INSERT INTO Wages
                                   (Id, Amount, EmployeeId, Description, `Date`, CreatedAt)
                                   VALUES (@Id, @Amount, @EmployeeId, @Description, @Date, @CreatedAt);";

            await connection.ExecuteAsync(query, wage);
            return wage;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = "DELETE FROM Wages WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }
}
