using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using Npgsql;
using System.Text.RegularExpressions;

namespace BakeFix.Repositories
{
    public class ProductRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public ProductRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        /// <summary>
        /// Builds a prefix-matching tsquery string equivalent to MySQL's
        /// MATCH ... AGAINST (... IN BOOLEAN MODE) with a trailing '*':
        /// each word becomes a `word:*` prefix term, ANDed together.
        /// </summary>
        private static string? BuildPrefixTsQuery(string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return null;
            var terms = search
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => Regex.Replace(w, @"[^\w]", ""))
                .Where(w => w.Length > 0)
                .Select(w => w + ":*")
                .ToArray();
            return terms.Length == 0 ? null : string.Join(" & ", terms);
        }

        public async Task<(IEnumerable<Product> Items, int TotalCount)> GetAllAsync(
            string? search, Guid? categoryId, bool includeInactive, int page, int pageSize)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var conditions = new List<string> { "p.OrganizationId = @orgId" };
            if (!includeInactive) conditions.Add("p.IsActive = TRUE");
            if (categoryId.HasValue) conditions.Add("p.CategoryId = @categoryId");
            var searchParam = BuildPrefixTsQuery(search);
            if (searchParam is not null)
                conditions.Add("p.SearchVector @@ to_tsquery('simple', @search)");

            var where = "WHERE " + string.Join(" AND ", conditions);

            var total = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Products p {where}",
                new { orgId, categoryId, search = searchParam });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<Product>(
                $@"SELECT p.Id, p.OrganizationId, p.CategoryId, c.Name AS CategoryName,
                          p.Name, p.Description, p.SKU, p.Unit,
                          p.CostPrice, p.SellingPrice, p.CurrentStock,
                          p.LowStockAlert, p.IsActive, p.CreatedAt, p.UpdatedAt
                   FROM Products p
                   LEFT JOIN ProductCategories c ON c.Id = p.CategoryId
                   {where}
                   ORDER BY p.Name ASC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, categoryId, search = searchParam, pageSize, offset });

            return (items, total);
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<Product>(
                @"SELECT p.Id, p.OrganizationId, p.CategoryId, c.Name AS CategoryName,
                         p.Name, p.Description, p.SKU, p.Unit,
                         p.CostPrice, p.SellingPrice, p.CurrentStock,
                         p.LowStockAlert, p.IsActive, p.CreatedAt, p.UpdatedAt
                  FROM Products p
                  LEFT JOIN ProductCategories c ON c.Id = p.CategoryId
                  WHERE p.Id = @id AND p.OrganizationId = @orgId",
                new { id, orgId = _tenant.RequiredOrgId });
        }

        public async Task<Product> CreateAsync(Product product)
        {
            using var connection = new NpgsqlConnection(_conn);
            product.OrganizationId = _tenant.RequiredOrgId;
            await connection.ExecuteAsync(
                @"INSERT INTO Products
                    (Id, OrganizationId, CategoryId, Name, Description, SKU, Unit,
                     CostPrice, SellingPrice, CurrentStock, LowStockAlert, IsActive, CreatedAt, UpdatedAt)
                  VALUES
                    (@Id, @OrganizationId, @CategoryId, @Name, @Description, @SKU, @Unit,
                     @CostPrice, @SellingPrice, 0, @LowStockAlert, TRUE, @CreatedAt, @UpdatedAt)",
                product);
            return product;
        }

        public async Task<bool> UpdateAsync(Product product)
        {
            using var connection = new NpgsqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                @"UPDATE Products
                  SET CategoryId = @CategoryId, Name = @Name, Description = @Description,
                      SKU = @SKU, Unit = @Unit, CostPrice = @CostPrice, SellingPrice = @SellingPrice,
                      LowStockAlert = @LowStockAlert, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND OrganizationId = @OrgId",
                new
                {
                    product.CategoryId, product.Name, product.Description,
                    product.SKU, product.Unit, product.CostPrice, product.SellingPrice,
                    product.LowStockAlert, product.UpdatedAt,
                    product.Id, OrgId = _tenant.RequiredOrgId
                });
            return rows > 0;
        }

        public async Task<bool> SetActiveAsync(Guid id, bool isActive)
        {
            using var connection = new NpgsqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                "UPDATE Products SET IsActive = @isActive, UpdatedAt = @now WHERE Id = @id AND OrganizationId = @orgId",
                new { isActive, now = DateTime.UtcNow, id, orgId = _tenant.RequiredOrgId });
            return rows > 0;
        }

        public async Task<InventorySummary> GetSummaryAsync()
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var stats = await connection.QuerySingleAsync<InventorySummary>(@"
                SELECT
                    COUNT(*)                                                                          AS TotalProducts,
                    SUM(CASE WHEN LowStockAlert IS NOT NULL AND CurrentStock <= LowStockAlert AND CurrentStock > 0 THEN 1 ELSE 0 END) AS LowStockCount,
                    SUM(CASE WHEN CurrentStock = 0 THEN 1 ELSE 0 END)                               AS OutOfStockCount,
                    COALESCE(SUM(CurrentStock * CostPrice), 0)                                       AS TotalInventoryValue
                FROM Products
                WHERE OrganizationId = @orgId AND IsActive = TRUE",
                new { orgId });

            var todayStart = _tenant.OrgLocalDate;
            var todayEnd   = todayStart.AddDays(1);

            var todaySales = await connection.QuerySingleAsync<(decimal Amount, int Count)>(@"
                SELECT COALESCE(SUM(TotalAmount), 0) AS Amount, COUNT(*) AS Count
                FROM StockTransactions
                WHERE OrganizationId = @orgId
                  AND Type = 'Sale'
                  AND Date >= @todayStart
                  AND Date < @todayEnd",
                new { orgId, todayStart, todayEnd });

            stats.TodaySalesAmount = todaySales.Amount;
            stats.TodaySalesCount  = todaySales.Count;

            return stats;
        }

        public async Task<IEnumerable<Product>> GetLowStockAsync()
        {
            using var connection = new NpgsqlConnection(_conn);
            return await connection.QueryAsync<Product>(
                @"SELECT p.Id, p.OrganizationId, p.CategoryId, c.Name AS CategoryName,
                         p.Name, p.SKU, p.Unit, p.CurrentStock, p.LowStockAlert, p.IsActive
                  FROM Products p
                  LEFT JOIN ProductCategories c ON c.Id = p.CategoryId
                  WHERE p.OrganizationId = @orgId
                    AND p.IsActive = TRUE
                    AND p.LowStockAlert IS NOT NULL
                    AND p.CurrentStock <= p.LowStockAlert
                  ORDER BY p.CurrentStock ASC",
                new { orgId = _tenant.RequiredOrgId });
        }
    }
}
