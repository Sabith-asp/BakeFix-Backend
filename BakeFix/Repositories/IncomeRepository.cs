using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class IncomeRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public IncomeRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<(IEnumerable<Income> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize, string? divisionId = null)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            const string where = @"WHERE i.OrganizationId = @orgId
                                     AND (@startDate IS NULL OR i.`Date` >= @startDate)
                                     AND (@endDate IS NULL OR i.`Date` <= @endDate)
                                     AND (@divisionId IS NULL OR i.DivisionId = @divisionId)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(i.Amount), 0) AS Amount FROM Incomes i {where}",
                new { orgId, startDate, endDate, divisionId });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Income>(
                $@"SELECT i.Id, i.OrganizationId, i.Amount, i.Description,
                          i.PaymentMethod, i.`Date`, i.CreatedAt, i.DivisionId,
                          d.Name AS DivisionName
                   FROM Incomes i
                   LEFT JOIN Divisions d ON d.Id = i.DivisionId
                   {where}
                   ORDER BY i.`Date` DESC, i.CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, startDate, endDate, divisionId, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<Income> CreateAsync(Income income)
        {
            using var connection = new MySqlConnection(_conn);
            income.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Incomes
                             (Id, OrganizationId, Amount, Description, PaymentMethod, DivisionId, `Date`, CreatedAt)
                             VALUES (@Id, @OrganizationId, @Amount, @Description, @PaymentMethod, @DivisionId, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, income);
            return income;
        }

        public async Task<bool> UpdateAsync(Income income)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Incomes
                                   SET Amount=@Amount, Description=@Description,
                                       PaymentMethod=@PaymentMethod, DivisionId=@DivisionId, `Date`=@Date
                                   WHERE Id=@Id AND OrganizationId=@OrgId";

            int rows = await connection.ExecuteAsync(query, new
            {
                income.Id,
                income.Amount,
                income.Description,
                income.PaymentMethod,
                income.DivisionId,
                income.Date,
                OrgId = _tenant.RequiredOrgId
            });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Incomes WHERE Id = @Id AND OrganizationId = @OrgId",
                new { Id = id, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
