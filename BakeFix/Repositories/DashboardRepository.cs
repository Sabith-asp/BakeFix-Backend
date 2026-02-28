using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class DashboardRepository
    {
        private readonly string _conn;

        public DashboardRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<DashboardSummary> GetSummaryAsync(DateTime? startDate, DateTime? endDate)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"SELECT
                                     COALESCE((SELECT SUM(Amount)
                                               FROM Incomes
                                               WHERE (@startDate IS NULL OR `Date` >= @startDate)
                                                 AND (@endDate IS NULL OR `Date` <= @endDate)), 0) AS TotalIncome,
                                     COALESCE((SELECT SUM(Amount)
                                               FROM Expenses
                                               WHERE (@startDate IS NULL OR `Date` >= @startDate)
                                                 AND (@endDate IS NULL OR `Date` <= @endDate)), 0) AS TotalExpense,
                                     COALESCE((SELECT SUM(Amount)
                                               FROM Wages
                                               WHERE (@startDate IS NULL OR `Date` >= @startDate)
                                                 AND (@endDate IS NULL OR `Date` <= @endDate)), 0) AS TotalWage;";

            return await connection.QuerySingleAsync<DashboardSummary>(query, new { startDate, endDate });
        }
    }
}
