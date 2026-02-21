using BakeFix.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BakeFix.Repositories
{
    public class IncomeRepository
    {
        private readonly string _conn;

        public IncomeRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Income>> GetAllAsync(DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_conn);

            string query = @"SELECT * FROM Incomes
                             WHERE (@startDate IS NULL OR Date >= @startDate)
                               AND (@endDate IS NULL OR Date <= @endDate)
                             ORDER BY CreatedAt DESC";

            return await connection.QueryAsync<Income>(query, new { startDate, endDate });
        }

        public async Task<Income> CreateAsync(Income income)
        {
            using var connection = new SqlConnection(_conn);

            string query = @"INSERT INTO Incomes (Id, Amount, Description, Date, CreatedAt)
                             VALUES (@Id, @Amount, @Description, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, income);
            return income;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new SqlConnection(_conn);

            string query = "DELETE FROM Incomes WHERE Id = @Id";

            int rows = await connection.ExecuteAsync(query, new { Id = id });
            return rows > 0;
        }
    }
}
