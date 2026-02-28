using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _service;

        public DashboardController(DashboardService service)
        {
            _service = service;
        }

        // GET /dashboard/summary?startDate=2024-01-01&endDate=2024-02-01
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            var summary = await _service.GetSummaryAsync(startDate, endDate);
            return Ok(summary);
        }
    }
}
