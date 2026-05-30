using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class InventoryService
    {
        private readonly ProductCategoryRepository _categoryRepo;
        private readonly ProductRepository _productRepo;
        private readonly StockTransactionRepository _txRepo;

        public InventoryService(
            ProductCategoryRepository categoryRepo,
            ProductRepository productRepo,
            StockTransactionRepository txRepo)
        {
            _categoryRepo = categoryRepo;
            _productRepo  = productRepo;
            _txRepo       = txRepo;
        }

        // ── Categories ───────────────────────────────────────────────────────

        public Task<IEnumerable<ProductCategory>> GetCategoriesAsync()
            => _categoryRepo.GetAllAsync();

        public Task<ProductCategory> CreateCategoryAsync(ProductCategoryFormData data)
        {
            if (string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("Category name is required.");

            return _categoryRepo.CreateAsync(new ProductCategory
            {
                Id        = Guid.NewGuid(),
                Name      = data.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<bool> UpdateCategoryAsync(Guid id, ProductCategoryFormData data)
        {
            if (string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("Category name is required.");

            return await _categoryRepo.UpdateAsync(id, data.Name.Trim());
        }

        public async Task DeleteCategoryAsync(Guid id)
        {
            if (await _categoryRepo.HasProductsAsync(id))
                throw new ArgumentException("Cannot delete: products are linked to this category.");

            await _categoryRepo.DeleteAsync(id);
        }

        // ── Products ─────────────────────────────────────────────────────────

        public async Task<PagedResult<Product>> GetProductsAsync(
            string? search, Guid? categoryId, bool includeInactive, int page, int pageSize)
        {
            int safePage     = Math.Max(1, page);
            int safePageSize = Math.Clamp(pageSize, 1, 100);

            var (items, totalCount) = await _productRepo.GetAllAsync(search, categoryId, includeInactive, safePage, safePageSize);

            return new PagedResult<Product>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = safePage,
                PageSize   = safePageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public Task<Product> CreateProductAsync(ProductFormData data)
        {
            if (string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("Product name is required.");

            if (string.IsNullOrWhiteSpace(data.Unit))
                throw new ArgumentException("Unit is required.");

            return _productRepo.CreateAsync(new Product
            {
                Id           = Guid.NewGuid(),
                CategoryId   = data.CategoryId,
                Name         = data.Name.Trim(),
                Description  = data.Description?.Trim(),
                SKU          = data.SKU?.Trim(),
                Unit         = data.Unit.Trim(),
                CostPrice    = data.CostPrice,
                SellingPrice = data.SellingPrice,
                LowStockAlert = data.LowStockAlert,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
        }

        public async Task<bool> UpdateProductAsync(Guid id, ProductFormData data)
        {
            if (string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("Product name is required.");

            if (string.IsNullOrWhiteSpace(data.Unit))
                throw new ArgumentException("Unit is required.");

            return await _productRepo.UpdateAsync(new Product
            {
                Id           = id,
                CategoryId   = data.CategoryId,
                Name         = data.Name.Trim(),
                Description  = data.Description?.Trim(),
                SKU          = data.SKU?.Trim(),
                Unit         = data.Unit.Trim(),
                CostPrice    = data.CostPrice,
                SellingPrice = data.SellingPrice,
                LowStockAlert = data.LowStockAlert,
                UpdatedAt    = DateTime.UtcNow
            });
        }

        public Task<bool> SetProductStatusAsync(Guid id, bool isActive)
            => _productRepo.SetActiveAsync(id, isActive);

        // ── Transactions ─────────────────────────────────────────────────────

        public async Task<PagedResult<StockTransaction>> GetTransactionsAsync(
            Guid? productId, string? type, string? startDate, string? endDate, int page, int pageSize)
        {
            int safePage     = Math.Max(1, page);
            int safePageSize = Math.Clamp(pageSize, 1, 100);

            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate)   ? null : DateTime.Parse(endDate);

            var (items, totalCount, totalAmount) = await _txRepo.GetAllAsync(productId, type, s, e, safePage, safePageSize);

            return new PagedResult<StockTransaction>
            {
                Items       = items,
                TotalCount  = totalCount,
                TotalAmount = totalAmount,
                Page        = safePage,
                PageSize    = safePageSize,
                TotalPages  = (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public async Task<StockTransaction> CreateTransactionAsync(StockTransactionFormData data)
        {
            if (data.Quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.");

            if (data.UnitPrice < 0)
                throw new ArgumentException("Unit price cannot be negative.");

            if (data.Type != "Purchase" && data.Type != "Sale")
                throw new ArgumentException("Type must be 'Purchase' or 'Sale'.");

            if (data.Type == "Sale")
            {
                var product = await _productRepo.GetByIdAsync(data.ProductId)
                    ?? throw new ArgumentException("Product not found.");

                if (data.Quantity > product.CurrentStock)
                    throw new ArgumentException(
                        $"Insufficient stock. Available: {product.CurrentStock} {product.Unit}.");
            }

            return await _txRepo.CreateAsync(new StockTransaction
            {
                Id                 = Guid.NewGuid(),
                ProductId          = data.ProductId,
                Type               = data.Type,
                Quantity           = data.Quantity,
                UnitPrice          = data.UnitPrice,
                SupplierOrCustomer = data.SupplierOrCustomer?.Trim(),
                PaymentMethod      = data.PaymentMethod,
                DivisionId         = data.DivisionId,
                Note               = data.Note?.Trim(),
                Date               = data.Date,
                CreatedAt          = DateTime.UtcNow
            });
        }

        public Task<(bool Success, string? Error)> DeleteTransactionAsync(Guid id)
            => _txRepo.DeleteAsync(id);

        // ── Summary ──────────────────────────────────────────────────────────

        public Task<InventorySummary> GetSummaryAsync()
            => _productRepo.GetSummaryAsync();

        public Task<IEnumerable<Product>> GetLowStockAsync()
            => _productRepo.GetLowStockAsync();
    }
}
