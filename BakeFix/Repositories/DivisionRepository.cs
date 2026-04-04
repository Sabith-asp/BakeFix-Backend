using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class DivisionRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public DivisionRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<IEnumerable<Division>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_conn);

            return await connection.QueryAsync<Division>(
                "SELECT Id, OrganizationId, Name, CreatedAt FROM Divisions WHERE OrganizationId = @OrgId ORDER BY Name ASC",
                new { OrgId = _tenant.RequiredOrgId });
        }

        public async Task<IEnumerable<Division>> GetByOrgIdAsync(Guid orgId)
        {
            using var connection = new MySqlConnection(_conn);

            return await connection.QueryAsync<Division>(
                "SELECT Id, OrganizationId, Name, CreatedAt FROM Divisions WHERE OrganizationId = @orgId ORDER BY Name ASC",
                new { orgId });
        }

        public async Task<Division> CreateAsync(Division division)
        {
            using var connection = new MySqlConnection(_conn);
            division.OrganizationId = _tenant.RequiredOrgId;

            const string query = @"INSERT INTO Divisions (Id, OrganizationId, Name, CreatedAt)
                                   VALUES (@Id, @OrganizationId, @Name, @CreatedAt)";

            await connection.ExecuteAsync(query, division);
            return division;
        }

        public async Task<bool> NameExistsAsync(string name)
        {
            using var connection = new MySqlConnection(_conn);

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Divisions WHERE LOWER(Name) = LOWER(@name) AND OrganizationId = @OrgId",
                new { name, OrgId = _tenant.RequiredOrgId });
            return count > 0;
        }

        public async Task<bool> NameExistsForOtherAsync(string name, Guid excludeId)
        {
            using var connection = new MySqlConnection(_conn);

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Divisions WHERE LOWER(Name) = LOWER(@name) AND Id != @excludeId AND OrganizationId = @OrgId",
                new { name, excludeId, OrgId = _tenant.RequiredOrgId });
            return count > 0;
        }

        public async Task<bool> UpdateAsync(Guid id, string name)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "UPDATE Divisions SET Name=@Name WHERE Id=@Id AND OrganizationId=@OrgId",
                new { Id = id, Name = name, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }

        public async Task<int> GetLinkedRecordCountAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT
                    (SELECT COUNT(1) FROM Expenses WHERE DivisionId = @id AND OrganizationId = @orgId) +
                    (SELECT COUNT(1) FROM Incomes  WHERE DivisionId = @id AND OrganizationId = @orgId) +
                    (SELECT COUNT(1) FROM Wages    WHERE DivisionId = @id AND OrganizationId = @orgId)",
                new { id, orgId });
            return count;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Divisions WHERE Id = @id AND OrganizationId = @OrgId",
                new { id, OrgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
