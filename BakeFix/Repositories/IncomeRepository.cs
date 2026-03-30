using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class IncomeRepository
    {
        private readonly string _conn;

        public IncomeRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<(IEnumerable<Income> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_conn);

            const string where = @"WHERE (@startDate IS NULL OR `Date` >= @startDate)
                                     AND (@endDate IS NULL OR `Date` <= @endDate)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(Amount), 0) AS Amount FROM Incomes {where}",
                new { startDate, endDate });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Income>(
                $@"SELECT * FROM Incomes {where}
                   ORDER BY `Date` DESC, CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { startDate, endDate, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<Income> CreateAsync(Income income)
        {
            using var connection = new MySqlConnection(_conn);

            string query = @"INSERT INTO Incomes 
                             (Id, Amount, Description, `Date`, CreatedAt)
                             VALUES (@Id, @Amount, @Description, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, income);
            return income;
        }

        public async Task<bool> UpdateAsync(Income income)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Incomes
                                   SET Amount=@Amount, Description=@Description, `Date`=@Date
                                   WHERE Id=@Id";

            int rows = await connection.ExecuteAsync(query, income);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            string query = "DELETE FROM Incomes WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }
}
