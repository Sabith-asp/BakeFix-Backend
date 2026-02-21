using BakeFix.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BakeFix.Repositories
{
    public class ExpenseRepository
    {
        private readonly string _conn;

        public ExpenseRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Expense>> GetAllAsync(DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_conn);

            string query = @"SELECT * FROM Expenses 
                             WHERE (@startDate IS NULL OR Date >= @startDate)
                               AND (@endDate IS NULL OR Date <= @endDate)
                             ORDER BY CreatedAt DESC";

            return await connection.QueryAsync<Expense>(query, new { startDate, endDate });
        }

        public async Task<Expense> CreateAsync(Expense expense)
        {
            using var connection = new SqlConnection(_conn);

            string query = @"INSERT INTO Expenses (Id, Amount, Description, Category, Date, CreatedAt)
                             VALUES (@Id, @Amount, @Description, @Category, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, expense);
            return expense;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new SqlConnection(_conn);

            string query = "DELETE FROM Expenses WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }

}
