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

        public async Task<IEnumerable<TrendDataPoint>> GetTrendAsync(int months)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"
                SELECT
                    DATE_FORMAT(`Date`, '%Y-%m') AS Month,
                    SUM(CASE WHEN type = 'income'  THEN Amount ELSE 0 END) AS Income,
                    SUM(CASE WHEN type = 'expense' THEN Amount ELSE 0 END) AS Expense,
                    SUM(CASE WHEN type = 'wage'    THEN Amount ELSE 0 END) AS Wage
                FROM (
                    SELECT `Date`, Amount, 'income'  AS type FROM Incomes
                    WHERE `Date` >= DATE_SUB(CURDATE(), INTERVAL @months MONTH)
                    UNION ALL
                    SELECT `Date`, Amount, 'expense' AS type FROM Expenses
                    WHERE `Date` >= DATE_SUB(CURDATE(), INTERVAL @months MONTH)
                    UNION ALL
                    SELECT `Date`, Amount, 'wage'    AS type FROM Wages
                    WHERE `Date` >= DATE_SUB(CURDATE(), INTERVAL @months MONTH)
                ) combined
                GROUP BY Month
                ORDER BY Month";

            var rawData = (await connection.QueryAsync<TrendDataPoint>(query, new { months }))
                          .ToDictionary(r => r.Month);

            var result = new List<TrendDataPoint>();
            bool showYear = months > 6;

            for (int i = months - 1; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var key   = date.ToString("yyyy-MM");
                var label = showYear ? date.ToString("MMM ''yy") : date.ToString("MMM");

                if (rawData.TryGetValue(key, out var point))
                {
                    point.Label = label;
                    result.Add(point);
                }
                else
                {
                    result.Add(new TrendDataPoint { Month = key, Label = label });
                }
            }

            return result;
        }
    }
}
