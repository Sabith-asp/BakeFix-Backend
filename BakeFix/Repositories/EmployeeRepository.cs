using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class EmployeeRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public EmployeeRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<IEnumerable<Employee>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_conn);

            return await connection.QueryAsync<Employee>(
                "SELECT Id, OrganizationId, Name, CreatedAt FROM Employees WHERE OrganizationId = @OrgId ORDER BY Name ASC",
                new { OrgId = _tenant.RequiredOrgId });
        }

        public async Task<Employee> CreateAsync(Employee employee)
        {
            using var connection = new MySqlConnection(_conn);
            employee.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Employees (Id, OrganizationId, Name, CreatedAt)
                                   VALUES (@Id, @OrganizationId, @Name, @CreatedAt)";

            await connection.ExecuteAsync(query, employee);
            return employee;
        }

        public async Task<bool> ExistsAsync(Guid employeeId)
        {
            using var connection = new MySqlConnection(_conn);

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Employees WHERE Id = @employeeId AND OrganizationId = @OrgId",
                new { employeeId, OrgId = _tenant.RequiredOrgId });
            return count > 0;
        }

        public async Task<bool> NameExistsAsync(string name)
        {
            using var connection = new MySqlConnection(_conn);

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Employees WHERE LOWER(Name) = LOWER(@name) AND OrganizationId = @OrgId",
                new { name, OrgId = _tenant.RequiredOrgId });
            return count > 0;
        }

        public async Task<bool> NameExistsForOtherAsync(string name, Guid excludeId)
        {
            using var connection = new MySqlConnection(_conn);

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Employees WHERE LOWER(Name) = LOWER(@name) AND Id != @excludeId AND OrganizationId = @OrgId",
                new { name, excludeId, OrgId = _tenant.RequiredOrgId });
            return count > 0;
        }

        public async Task<bool> UpdateAsync(Guid id, string name)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "UPDATE Employees SET Name=@Name WHERE Id=@Id AND OrganizationId=@OrgId",
                new { Id = id, Name = name, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }

        public async Task<int> GetWageCountAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);

            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Wages WHERE EmployeeId = @id AND OrganizationId = @OrgId",
                new { id, OrgId = _tenant.RequiredOrgId });
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Employees WHERE Id = @id AND OrganizationId = @OrgId",
                new { id, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
