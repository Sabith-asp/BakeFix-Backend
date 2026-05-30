using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class ProductCategoryRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public ProductCategoryRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<IEnumerable<ProductCategory>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<ProductCategory>(
                "SELECT Id, OrganizationId, Name, CreatedAt FROM ProductCategories WHERE OrganizationId = @orgId ORDER BY Name ASC",
                new { orgId = _tenant.RequiredOrgId });
        }

        public async Task<ProductCategory> CreateAsync(ProductCategory category)
        {
            using var connection = new MySqlConnection(_conn);
            category.OrganizationId = _tenant.RequiredOrgId;
            await connection.ExecuteAsync(
                "INSERT INTO ProductCategories (Id, OrganizationId, Name, CreatedAt) VALUES (@Id, @OrganizationId, @Name, @CreatedAt)",
                category);
            return category;
        }

        public async Task<bool> UpdateAsync(Guid id, string name)
        {
            using var connection = new MySqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                "UPDATE ProductCategories SET Name = @name WHERE Id = @id AND OrganizationId = @orgId",
                new { name, id, orgId = _tenant.RequiredOrgId });
            return rows > 0;
        }

        public async Task<bool> HasProductsAsync(Guid categoryId)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Products WHERE CategoryId = @categoryId AND OrganizationId = @orgId",
                new { categoryId, orgId = _tenant.RequiredOrgId }) > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                "DELETE FROM ProductCategories WHERE Id = @id AND OrganizationId = @orgId",
                new { id, orgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
