using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("wage")]
    [Authorize]
    [RequireModule("Wages")]
    public class WageController : ControllerBase
    {
        private readonly WageService _service;

        public WageController(WageService service)
        {
            _service = service;
        }

        // GET /wage?startDate=2024-01-01&endDate=2024-02-01&page=1&pageSize=20&employeeId=guid&divisionId=guid
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? employeeId = null,
            [FromQuery] string? divisionId = null)
        {
            var result = await _service.GetAllAsync(startDate, endDate, page, pageSize, employeeId, divisionId);
            return Ok(result);
        }

        // GET /wage/employee-summary?startDate=2024-01-01&endDate=2024-02-01
        [HttpGet("employee-summary")]
        public async Task<IActionResult> GetEmployeeSummary(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            var result = await _service.GetEmployeeSummaryAsync(startDate, endDate);
            return Ok(result);
        }

        // POST /wage
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WageFormData request)
        {
            try
            {
                var wage = await _service.CreateAsync(request);
                return Ok(wage);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /wage/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] WageFormData request)
        {
            try
            {
                var success = await _service.UpdateAsync(id, request);

                if (!success)
                    return NotFound(new { message = "Wage not found" });

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /wage/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Wage not found" });

            return NoContent();
        }
    }
}
