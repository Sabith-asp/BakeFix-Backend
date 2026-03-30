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

        public async Task<(IEnumerable<Wage> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_conn);

            const string where = @"WHERE (@startDate IS NULL OR w.`Date` >= @startDate)
                                     AND (@endDate IS NULL OR w.`Date` <= @endDate)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(w.Amount), 0) AS Amount FROM Wages w {where}",
                new { startDate, endDate });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Wage>(
                $@"SELECT w.Id,
                          w.Amount,
                          w.EmployeeId,
                          e.Name AS EmployeeName,
                          w.Description,
                          w.`Date`,
                          w.CreatedAt
                   FROM Wages w
                   LEFT JOIN Employees e ON e.Id = w.EmployeeId
                   {where}
                   ORDER BY w.`Date` DESC, w.CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { startDate, endDate, pageSize, offset });

            return (items, summary.Count, summary.Amount);
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

        public async Task<bool> UpdateAsync(Wage wage)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Wages
                                   SET Amount=@Amount, EmployeeId=@EmployeeId, Description=@Description, `Date`=@Date
                                   WHERE Id=@Id";

            int rows = await connection.ExecuteAsync(query, wage);
            return rows > 0;
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
