using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class WageRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public WageRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<(IEnumerable<Wage> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            DateTime? startDate, DateTime? endDate, int page, int pageSize, string? employeeId = null)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            const string where = @"WHERE w.OrganizationId = @orgId
                                     AND (@startDate IS NULL OR w.`Date` >= @startDate)
                                     AND (@endDate IS NULL OR w.`Date` <= @endDate)
                                     AND (@employeeId IS NULL OR w.EmployeeId = @employeeId)";

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(w.Amount), 0) AS Amount FROM Wages w {where}",
                new { orgId, startDate, endDate, employeeId });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Wage>(
                $@"SELECT w.Id,
                          w.OrganizationId,
                          w.Amount,
                          w.EmployeeId,
                          e.Name AS EmployeeName,
                          w.Description,
                          w.PaymentMethod,
                          w.`Date`,
                          w.CreatedAt
                   FROM Wages w
                   LEFT JOIN Employees e ON e.Id = w.EmployeeId
                   {where}
                   ORDER BY w.`Date` DESC, w.CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, startDate, endDate, employeeId, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<IEnumerable<EmployeeWageSummary>> GetEmployeeSummaryAsync(DateTime? startDate, DateTime? endDate)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            const string query = @"
                SELECT
                    w.EmployeeId,
                    e.Name AS EmployeeName,
                    COUNT(*) AS RecordCount,
                    COALESCE(SUM(w.Amount), 0) AS TotalAmount
                FROM Wages w
                LEFT JOIN Employees e ON e.Id = w.EmployeeId
                WHERE w.OrganizationId = @orgId
                  AND (@startDate IS NULL OR w.`Date` >= @startDate)
                  AND (@endDate IS NULL OR w.`Date` <= @endDate)
                GROUP BY w.EmployeeId, e.Name
                ORDER BY TotalAmount DESC";

            return await connection.QueryAsync<EmployeeWageSummary>(query, new { orgId, startDate, endDate });
        }

        public async Task<Wage> CreateAsync(Wage wage)
        {
            using var connection = new MySqlConnection(_conn);
            wage.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Wages
                                   (Id, OrganizationId, Amount, EmployeeId, Description, PaymentMethod, `Date`, CreatedAt)
                                   VALUES (@Id, @OrganizationId, @Amount, @EmployeeId, @Description, @PaymentMethod, @Date, @CreatedAt)";

            await connection.ExecuteAsync(query, wage);
            return wage;
        }

        public async Task<bool> UpdateAsync(Wage wage)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"UPDATE Wages
                                   SET Amount=@Amount, EmployeeId=@EmployeeId, Description=@Description, PaymentMethod=@PaymentMethod, `Date`=@Date
                                   WHERE Id=@Id AND OrganizationId=@OrgId";

            int rows = await connection.ExecuteAsync(query, new
            {
                wage.Id,
                wage.Amount,
                wage.EmployeeId,
                wage.Description,
                wage.PaymentMethod,
                wage.Date,
                OrgId = _tenant.RequiredOrgId
            });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Wages WHERE Id = @Id AND OrganizationId = @OrgId",
                new { Id = id, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
