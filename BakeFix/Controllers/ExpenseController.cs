using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("expense")]
    [Authorize]
    public class ExpenseController : ControllerBase
    {
        private readonly ExpenseService _service;

        public ExpenseController(ExpenseService service)
        {
            _service = service;
        }

        // GET /expense?startDate=2024-01-01&endDate=2024-02-01
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] int? limit)
        {
            var expenses = await _service.GetAllAsync(startDate, endDate, limit);
            return Ok(expenses);
        }

        // POST /expense
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExpenseFormData request)
        {
            var expense = await _service.CreateAsync(request);
            return Ok(expense);
        }

        // DELETE /expense/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Expense not found" });

            return NoContent();
        }
    }
}
