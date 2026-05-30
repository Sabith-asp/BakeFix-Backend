using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class StockTransactionRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public StockTransactionRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<(IEnumerable<StockTransaction> Items, int TotalCount, decimal TotalAmount)> GetAllAsync(
            Guid? productId, string? type, DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var conditions = new List<string> { "t.OrganizationId = @orgId" };
            if (productId.HasValue) conditions.Add("t.ProductId = @productId");
            if (!string.IsNullOrWhiteSpace(type)) conditions.Add("t.Type = @type");
            if (startDate.HasValue) conditions.Add("t.Date >= @startDate");
            if (endDate.HasValue) conditions.Add("t.Date <= @endDate");

            var where = "WHERE " + string.Join(" AND ", conditions);

            var summary = await connection.QuerySingleAsync<(int Count, decimal Amount)>(
                $"SELECT COUNT(*) AS Count, COALESCE(SUM(t.TotalAmount), 0) AS Amount FROM StockTransactions t {where}",
                new { orgId, productId, type, startDate, endDate });

            int offset = (page - 1) * pageSize;
            var items = await connection.QueryAsync<StockTransaction>(
                $@"SELECT t.Id, t.OrganizationId, t.ProductId, p.Name AS ProductName, p.Unit AS ProductUnit,
                          t.Type, t.Quantity, t.UnitPrice, t.TotalAmount,
                          t.SupplierOrCustomer, t.PaymentMethod, t.DivisionId,
                          d.Name AS DivisionName, t.Note, t.Date, t.CreatedAt
                   FROM StockTransactions t
                   JOIN Products p ON p.Id = t.ProductId
                   LEFT JOIN Divisions d ON d.Id = t.DivisionId
                   {where}
                   ORDER BY t.Date DESC, t.CreatedAt DESC
                   LIMIT @pageSize OFFSET @offset",
                new { orgId, productId, type, startDate, endDate, pageSize, offset });

            return (items, summary.Count, summary.Amount);
        }

        public async Task<StockTransaction> CreateAsync(StockTransaction transaction)
        {
            using var connection = new MySqlConnection(_conn);
            connection.Open();
            using var dbTx = connection.BeginTransaction();

            try
            {
                transaction.OrganizationId = _tenant.RequiredOrgId;
                transaction.TotalAmount    = transaction.Quantity * transaction.UnitPrice;

                await connection.ExecuteAsync(
                    @"INSERT INTO StockTransactions
                        (Id, OrganizationId, ProductId, Type, Quantity, UnitPrice, TotalAmount,
                         SupplierOrCustomer, PaymentMethod, DivisionId, Note, Date, CreatedAt)
                      VALUES
                        (@Id, @OrganizationId, @ProductId, @Type, @Quantity, @UnitPrice, @TotalAmount,
                         @SupplierOrCustomer, @PaymentMethod, @DivisionId, @Note, @Date, @CreatedAt)",
                    transaction, dbTx);

                decimal delta = transaction.Type == "Purchase" ? transaction.Quantity : -transaction.Quantity;

                await connection.ExecuteAsync(
                    "UPDATE Products SET CurrentStock = CurrentStock + @delta, UpdatedAt = @now WHERE Id = @productId",
                    new { delta, now = DateTime.UtcNow, productId = transaction.ProductId },
                    dbTx);

                dbTx.Commit();
                return transaction;
            }
            catch
            {
                dbTx.Rollback();
                throw;
            }
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            connection.Open();
            using var dbTx = connection.BeginTransaction();

            try
            {
                var tx = await connection.QueryFirstOrDefaultAsync<StockTransaction>(
                    "SELECT * FROM StockTransactions WHERE Id = @id AND OrganizationId = @orgId",
                    new { id, orgId = _tenant.RequiredOrgId }, dbTx);

                if (tx is null)
                {
                    dbTx.Rollback();
                    return (false, null);
                }

                // Deleting a Purchase reduces stock — ensure it won't go negative
                if (tx.Type == "Purchase")
                {
                    var currentStock = await connection.ExecuteScalarAsync<decimal>(
                        "SELECT CurrentStock FROM Products WHERE Id = @productId",
                        new { productId = tx.ProductId }, dbTx);

                    if (currentStock - tx.Quantity < 0)
                    {
                        dbTx.Rollback();
                        return (false, $"Cannot delete: removing this purchase would make the stock negative (current stock: {currentStock}).");
                    }
                }

                await connection.ExecuteAsync(
                    "DELETE FROM StockTransactions WHERE Id = @id",
                    new { id }, dbTx);

                decimal delta = tx.Type == "Purchase" ? -tx.Quantity : tx.Quantity;

                await connection.ExecuteAsync(
                    "UPDATE Products SET CurrentStock = CurrentStock + @delta, UpdatedAt = @now WHERE Id = @productId",
                    new { delta, now = DateTime.UtcNow, productId = tx.ProductId },
                    dbTx);

                dbTx.Commit();
                return (true, null);
            }
            catch
            {
                dbTx.Rollback();
                throw;
            }
        }
    }
}
