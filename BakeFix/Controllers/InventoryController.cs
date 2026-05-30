using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("inventory")]
    [Authorize]
    [RequireModule("Inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryService _service;

        public InventoryController(InventoryService service)
        {
            _service = service;
        }

        // ── Categories ───────────────────────────────────────────────────────

        // GET /inventory/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
            => Ok(await _service.GetCategoriesAsync());

        // POST /inventory/categories
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] ProductCategoryFormData data)
        {
            var category = await _service.CreateCategoryAsync(data);
            return Ok(category);
        }

        // PUT /inventory/categories/{id}
        [HttpPut("categories/{id:guid}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] ProductCategoryFormData data)
        {
            var success = await _service.UpdateCategoryAsync(id, data);
            if (!success) return NotFound(new { message = "Category not found." });
            return NoContent();
        }

        // DELETE /inventory/categories/{id}
        [HttpDelete("categories/{id:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            await _service.DeleteCategoryAsync(id);
            return NoContent();
        }

        // ── Products ─────────────────────────────────────────────────────────

        // GET /inventory/products?search=&categoryId=&page=1&pageSize=20&includeInactive=false
        [HttpGet("products")]
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

        // POST /inventory/products
        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductFormData data)
        {
            var product = await _service.CreateProductAsync(data);
            return Ok(product);
        }

        // PUT /inventory/products/{id}
        [HttpPut("products/{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductFormData data)
        {
            var success = await _service.UpdateProductAsync(id, data);
            if (!success) return NotFound(new { message = "Product not found." });
            return NoContent();
        }

        // PATCH /inventory/products/{id}/status
        [HttpPatch("products/{id:guid}/status")]
        public async Task<IActionResult> SetProductStatus(Guid id, [FromBody] SetProductStatusRequest request)
        {
            var success = await _service.SetProductStatusAsync(id, request.IsActive);
            if (!success) return NotFound(new { message = "Product not found." });
            return NoContent();
        }

        // ── Transactions ─────────────────────────────────────────────────────

        // GET /inventory/transactions?productId=&type=&startDate=&endDate=&page=1&pageSize=20
        [HttpGet("transactions")]
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

        // POST /inventory/transactions
        [HttpPost("transactions")]
        public async Task<IActionResult> CreateTransaction([FromBody] StockTransactionFormData data)
        {
            var tx = await _service.CreateTransactionAsync(data);
            return Ok(tx);
        }

        // DELETE /inventory/transactions/{id}
        [HttpDelete("transactions/{id:guid}")]
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

        // GET /inventory/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
            => Ok(await _service.GetSummaryAsync());

        // GET /inventory/low-stock
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock()
            => Ok(await _service.GetLowStockAsync());
    }
}
