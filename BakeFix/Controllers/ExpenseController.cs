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

        // GET /expense?startDate=2024-01-01&endDate=2024-02-01&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetAllAsync(startDate, endDate, page, pageSize);
            return Ok(result);
        }

        // POST /expense
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExpenseFormData request)
        {
            var expense = await _service.CreateAsync(request);
            return Ok(expense);
        }

        // PUT /expense/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ExpenseFormData request)
        {
            var success = await _service.UpdateAsync(id, request);

            if (!success)
                return NotFound(new { message = "Expense not found" });

            return NoContent();
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
