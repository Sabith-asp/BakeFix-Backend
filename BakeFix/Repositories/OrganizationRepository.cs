using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public interface IOrganizationRepository
    {
        Task<IEnumerable<Organization>> GetAllAsync();
        Task<Organization?> GetByIdAsync(Guid id);
        Task<Organization> CreateAsync(Organization org);
        Task SetActiveAsync(Guid id, bool isActive);
        Task<List<string>> GetEnabledModulesAsync(Guid orgId);
        Task SetModuleEnabledAsync(Guid orgId, string moduleName, bool enabled);
    }

    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly string _conn;

        public OrganizationRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IEnumerable<Organization>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_conn);

            var orgs = (await connection.QueryAsync<Organization>(
                "SELECT Id, Name, Slug, IsActive, CreatedAt FROM Organizations ORDER BY CreatedAt DESC"))
                .ToList();

            if (!orgs.Any()) return orgs;

            var modules = await connection.QueryAsync<(Guid OrgId, string ModuleName)>(
                @"SELECT om.OrganizationId AS OrgId, m.Name AS ModuleName
                  FROM OrganizationModules om
                  JOIN Modules m ON m.Id = om.ModuleId
                  WHERE om.IsEnabled = TRUE");

            var moduleMap = modules
                .GroupBy(x => x.OrgId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ModuleName).ToList());

            foreach (var org in orgs)
                org.EnabledModules = moduleMap.TryGetValue(org.Id, out var list) ? list : new();

            return orgs;
        }

        public async Task<Organization?> GetByIdAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);

            var org = await connection.QueryFirstOrDefaultAsync<Organization>(
                "SELECT Id, Name, Slug, IsActive, CreatedAt FROM Organizations WHERE Id = @id",
                new { id });

            if (org is null) return null;

            org.EnabledModules = await GetEnabledModulesAsync(id);
            return org;
        }

        public async Task<Organization> CreateAsync(Organization org)
        {
            using var connection = new MySqlConnection(_conn);

            org.Id = Guid.NewGuid();
            org.IsActive = true;
            org.CreatedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(
                "INSERT INTO Organizations (Id, Name, Slug, IsActive, CreatedAt) VALUES (@Id, @Name, @Slug, @IsActive, @CreatedAt)",
                org);

            // Income + Expenses are always enabled for every new org
            await connection.ExecuteAsync(
                @"INSERT INTO OrganizationModules (OrganizationId, ModuleId, IsEnabled)
                  SELECT @OrgId, Id, TRUE FROM Modules WHERE Name IN ('Income', 'Expenses')",
                new { OrgId = org.Id });

            // Wages, Employees, Divisions, Notifications, Debts start disabled — SuperAdmin must enable them explicitly
            await connection.ExecuteAsync(
                @"INSERT INTO OrganizationModules (OrganizationId, ModuleId, IsEnabled)
                  SELECT @OrgId, Id, FALSE FROM Modules WHERE Name IN ('Wages', 'Employees', 'Divisions', 'Notifications', 'Debts')",
                new { OrgId = org.Id });

            org.EnabledModules = new List<string> { "Income", "Expenses" };
            return org;
        }

        public async Task SetActiveAsync(Guid id, bool isActive)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                "UPDATE Organizations SET IsActive = @isActive WHERE Id = @id",
                new { id, isActive });
        }

        public async Task<List<string>> GetEnabledModulesAsync(Guid orgId)
        {
            using var connection = new MySqlConnection(_conn);

            var modules = await connection.QueryAsync<string>(
                @"SELECT m.Name
                  FROM OrganizationModules om
                  JOIN Modules m ON m.Id = om.ModuleId
                  WHERE om.OrganizationId = @orgId AND om.IsEnabled = TRUE",
                new { orgId });

            return modules.ToList();
        }

        public async Task SetModuleEnabledAsync(Guid orgId, string moduleName, bool enabled)
        {
            using var connection = new MySqlConnection(_conn);

            await connection.ExecuteAsync(
                @"UPDATE OrganizationModules om
                  JOIN Modules m ON m.Id = om.ModuleId
                  SET om.IsEnabled = @enabled
                  WHERE om.OrganizationId = @orgId AND m.Name = @moduleName",
                new { orgId, moduleName, enabled });
        }
    }
}
