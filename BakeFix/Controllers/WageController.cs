using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("wage")]
    [Authorize]
    public class WageController : ControllerBase
    {
        private readonly WageService _service;

        public WageController(WageService service)
        {
            _service = service;
        }

        // GET /wage?startDate=2024-01-01&endDate=2024-02-01
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            var wages = await _service.GetAllAsync(startDate, endDate);
            return Ok(wages);
        }

        // POST /wage
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WageFormData request)
        {
            var wage = await _service.CreateAsync(request);
            return Ok(wage);
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
