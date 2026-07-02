using BakeFix.DTOs;
using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Product inventory management. Requires the <b>Inventory</b> module to be enabled.</summary>
    /// <remarks>
    /// The inventory module tracks products with stock levels, categories, and a ledger of
    /// Purchase/Sale transactions. Every transaction atomically updates <c>CurrentStock</c>.
    /// </remarks>
    [ApiController]
    [Route("inventory")]
    [Authorize]
    [RequireModule("Inventory")]
    [Produces("application/json")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryService _service;

        public InventoryController(InventoryService service)
        {
            _service = service;
        }

        // ── Categories ───────────────────────────────────────────────────────

        /// <summary>List all product categories for the organisation.</summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(IEnumerable<ProductCategory>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCategories()
            => Ok(await _service.GetCategoriesAsync());

        /// <summary>Create a new product category.</summary>
        /// <param name="data">Category name.</param>
        [HttpPost("categories")]
        [ProducesResponseType(typeof(ProductCategory), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateCategory([FromBody] ProductCategoryFormData data)
        {
            var category = await _service.CreateCategoryAsync(data);
            return Ok(category);
        }

        /// <summary>Rename a product category.</summary>
        /// <param name="id">Category ID.</param>
        /// <param name="data">New category name.</param>
        [HttpPut("categories/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] ProductCategoryFormData data)
        {
            var success = await _service.UpdateCategoryAsync(id, data);
            if (!success) return NotFound(new { message = "Category not found." });
            return NoContent();
        }

        /// <summary>Delete a product category.</summary>
        /// <remarks>Returns <c>400</c> if any products are still assigned to this category.</remarks>
        /// <param name="id">Category ID.</param>
        [HttpDelete("categories/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            await _service.DeleteCategoryAsync(id);
            return NoContent();
        }

        // ── Products ─────────────────────────────────────────────────────────

        /// <summary>List products with optional search, category, and pagination filters.</summary>
        /// <remarks>
        /// <c>search</c> performs a FULLTEXT search on product name and description.
        /// Only active products are returned unless <c>includeInactive=true</c>.
        /// </remarks>
        /// <param name="search">Search term matched against name and description. Optional.</param>
        /// <param name="categoryId">Filter by category ID. Optional.</param>
        /// <param name="includeInactive">Include deactivated products (default false).</param>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Records per page (default 20).</param>
        [HttpGet("products")]
        [ProducesResponseType(typeof(PagedResult<Product>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool includeInactive = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetProductsAsync(search, categoryId, includeInactive, page, pageSize);
            return Ok(result);
        }

        /// <summary>Create a new product.</summary>
        /// <remarks>Initial <c>CurrentStock</c> is 0; adjust via stock transactions.</remarks>
        /// <param name="data">Product details.</param>
        [HttpPost("products")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductFormData data)
        {
            var product = await _service.CreateProductAsync(data);
            return Ok(product);
        }

        /// <summary>Update a product's details.</summary>
        /// <remarks>Does not affect stock levels; use transactions for stock changes.</remarks>
        /// <param name="id">Product ID.</param>
        /// <param name="data">Updated product details.</param>
        [HttpPut("products/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductFormData data)
        {
            var success = await _service.UpdateProductAsync(id, data);
            if (!success) return NotFound(new { message = "Product not found." });
            return NoContent();
        }

        /// <summary>Activate or deactivate a product.</summary>
        /// <remarks>Deactivated products are hidden from the default product list but retain their history.</remarks>
        /// <param name="id">Product ID.</param>
        /// <param name="request">Desired active state.</param>
        [HttpPatch("products/{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetProductStatus(Guid id, [FromBody] SetProductStatusRequest request)
        {
            var success = await _service.SetProductStatusAsync(id, request.IsActive);
            if (!success) return NotFound(new { message = "Product not found." });
            return NoContent();
        }

        // ── Transactions ─────────────────────────────────────────────────────

        /// <summary>List stock transactions with optional product, type, date, and pagination filters.</summary>
        /// <param name="productId">Filter by product ID. Optional.</param>
        /// <param name="type">Filter by type: <c>Purchase</c> or <c>Sale</c>. Optional.</param>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Records per page (default 20).</param>
        [HttpGet("transactions")]
        [ProducesResponseType(typeof(PagedResult<StockTransaction>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] Guid? productId,
            [FromQuery] string? type,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetTransactionsAsync(productId, type, startDate, endDate, page, pageSize);
            return Ok(result);
        }

        /// <summary>Record a stock transaction (Purchase or Sale).</summary>
        /// <remarks>
        /// <c>Type</c> must be <c>Purchase</c> or <c>Sale</c>.
        /// A <b>Sale</b> will be rejected with <c>400</c> if the quantity exceeds current stock.
        /// The product's <c>CurrentStock</c> is updated atomically within the same database transaction.
        /// </remarks>
        /// <param name="data">Transaction details.</param>
        [HttpPost("transactions")]
        [ProducesResponseType(typeof(StockTransaction), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateTransaction([FromBody] StockTransactionFormData data)
        {
            var tx = await _service.CreateTransactionAsync(data);
            return Ok(tx);
        }

        /// <summary>Delete a stock transaction and reverse the stock adjustment.</summary>
        /// <remarks>
        /// Deleting a <b>Purchase</b> transaction will be rejected with <c>400</c> if reversing
        /// it would cause the product's stock to go negative.
        /// </remarks>
        /// <param name="id">Transaction ID.</param>
        [HttpDelete("transactions/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTransaction(Guid id)
        {
            var (success, error) = await _service.DeleteTransactionAsync(id);

            if (!success && error is not null)
                return BadRequest(new { message = error });

            if (!success)
                return NotFound(new { message = "Transaction not found." });

            return NoContent();
        }

        // ── Summary ──────────────────────────────────────────────────────────

        /// <summary>Get a high-level inventory summary for the organisation.</summary>
        /// <remarks>
        /// Includes total product count, low-stock and out-of-stock counts,
        /// total inventory value (at cost price), and today's sales.
        /// </remarks>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(InventorySummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSummary()
            => Ok(await _service.GetSummaryAsync());

        /// <summary>Get all products whose current stock is at or below the low-stock alert threshold.</summary>
        [HttpGet("low-stock")]
        [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetLowStock()
            => Ok(await _service.GetLowStockAsync());
    }
}
