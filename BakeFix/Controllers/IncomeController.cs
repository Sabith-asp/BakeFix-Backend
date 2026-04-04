using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("income")]
    [Authorize]
    public class IncomeController : ControllerBase
    {
        private readonly IncomeService _service;

        public IncomeController(IncomeService service)
        {
            _service = service;
        }

        // GET /income?startDate=2024-01-01&endDate=2024-02-01&page=1&pageSize=20&divisionId=guid
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? divisionId = null)
        {
            var result = await _service.GetAllAsync(startDate, endDate, page, pageSize, divisionId);
            return Ok(result);
        }

        // POST /income
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] IncomeFormData request)
        {
            var income = await _service.CreateAsync(request);
            return Ok(income);
        }

        // PUT /income/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] IncomeFormData request)
        {
            var success = await _service.UpdateAsync(id, request);

            if (!success)
                return NotFound(new { message = "Income not found" });

            return NoContent();
        }

        // DELETE /income/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Income not found" });

            return NoContent();
        }
    }
}
