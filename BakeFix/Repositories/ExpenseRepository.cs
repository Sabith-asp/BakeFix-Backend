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
            DateTime? startDate, DateTime? endDate, int page, int pageSize, string? category = null, string? divisionId = null)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            const string where = @"WHERE e.OrganizationId = @orgId
                                     AND (@startDate IS NULL OR e.`Date` >= @startDate)
                                     AND (@endDate IS NULL OR e.`Date` <= @endDate)
                                     AND (@category IS NULL OR e.Category = @category)
                                     AND (@divisionId IS NULL OR e.DivisionId = @divisionId)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(e.Amount), 0) AS Amount FROM Expenses e {where}",
                new { orgId, startDate, endDate, category, divisionId });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Expense>(
                $@"SELECT e.Id, e.OrganizationId, e.Amount, e.Description, e.Category,
                          e.PaymentMethod, e.`Date`, e.CreatedAt, e.DivisionId,
                          d.Name AS DivisionName
                   FROM Expenses e
                   LEFT JOIN Divisions d ON d.Id = e.DivisionId
                   {where}
                   ORDER BY e.`Date` DESC, e.CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, startDate, endDate, category, divisionId, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<Expense> CreateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);
            expense.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Expenses
                             (Id, OrganizationId, Amount, Description, Category, PaymentMethod, DivisionId, `Date`, CreatedAt)
                             VALUES (@Id, @OrganizationId, @Amount, @Description, @Category, @PaymentMethod, @DivisionId, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, expense);
            return expense;
        }

        public async Task<bool> UpdateAsync(Expense expense)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Expenses
                                   SET Amount=@Amount, Description=@Description, Category=@Category,
                                       PaymentMethod=@PaymentMethod, DivisionId=@DivisionId, `Date`=@Date
                                   WHERE Id=@Id AND OrganizationId=@OrgId";

            int rows = await connection.ExecuteAsync(query, new
            {
                expense.Id,
                expense.Amount,
                expense.Description,
                expense.Category,
                expense.PaymentMethod,
                expense.DivisionId,
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
