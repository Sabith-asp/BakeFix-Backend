using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class ExpenseRepository
    {
        private readonly string _conn;

        public ExpenseRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<(IEnumerable<Expense> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_conn);

            const string where = @"WHERE (@startDate IS NULL OR `Date` >= @startDate)
                                     AND (@endDate IS NULL OR `Date` <= @endDate)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(Amount), 0) AS Amount FROM Expenses {where}",
                new { startDate, endDate });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Expense>(
                $@"SELECT * FROM Expenses {where}
                   ORDER BY `Date` DESC, CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { startDate, endDate, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<Expense> CreateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);

            string query = @"INSERT INTO Expenses 
                             (Id, Amount, Description, Category, `Date`, CreatedAt)
                             VALUES (@Id, @Amount, @Description, @Category, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, expense);
            return expense;
        }

        public async Task<bool> UpdateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Expenses
                                   SET Amount=@Amount, Description=@Description, Category=@Category, `Date`=@Date
                                   WHERE Id=@Id";

            int rows = await connection.ExecuteAsync(query, expense);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            string query = "DELETE FROM Expenses WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }
}
