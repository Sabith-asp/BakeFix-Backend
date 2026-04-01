using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class ExpenseRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public ExpenseRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<(IEnumerable<Expense> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize, string? category = null)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            const string where = @"WHERE OrganizationId = @orgId
                                     AND (@startDate IS NULL OR `Date` >= @startDate)
                                     AND (@endDate IS NULL OR `Date` <= @endDate)
                                     AND (@category IS NULL OR Category = @category)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(Amount), 0) AS Amount FROM Expenses {where}",
                new { orgId, startDate, endDate, category });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Expense>(
                $@"SELECT * FROM Expenses {where}
                   ORDER BY `Date` DESC, CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, startDate, endDate, category, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<Expense> CreateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);
            expense.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Expenses
                             (Id, OrganizationId, Amount, Description, Category, PaymentMethod, `Date`, CreatedAt)
                             VALUES (@Id, @OrganizationId, @Amount, @Description, @Category, @PaymentMethod, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, expense);
            return expense;
        }

        public async Task<bool> UpdateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Expenses
                                   SET Amount=@Amount, Description=@Description, Category=@Category, PaymentMethod=@PaymentMethod, `Date`=@Date
                                   WHERE Id=@Id AND OrganizationId=@OrgId";

            int rows = await connection.ExecuteAsync(query, new
            {
                expense.Id,
                expense.Amount,
                expense.Description,
                expense.Category,
                expense.PaymentMethod,
                expense.Date,
                OrgId = _tenant.RequiredOrgId
            });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Expenses WHERE Id = @Id AND OrganizationId = @OrgId",
                new { Id = id, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
