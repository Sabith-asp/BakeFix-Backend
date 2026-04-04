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

        // GET /dashboard/summary?startDate=2024-01-01&endDate=2024-02-01&divisionId=guid
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? divisionId = null)
        {
            var summary = await _service.GetSummaryAsync(startDate, endDate, divisionId);
            return Ok(summary);
        }

        // GET /dashboard/trend?months=6&divisionId=guid
        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend(
            [FromQuery] int months = 6,
            [FromQuery] string? divisionId = null)
        {
            var result = await _service.GetTrendAsync(months, divisionId);
            return Ok(result);
        }
    }
}
