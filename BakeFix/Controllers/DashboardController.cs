using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Financial summary and trend analytics for the caller's organisation.</summary>
    [ApiController]
    [Route("dashboard")]
    [Authorize]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _service;

        public DashboardController(DashboardService service)
        {
            _service = service;
        }

        /// <summary>Get aggregated totals for income, expenses, and wages.</summary>
        /// <remarks>
        /// Omitting <c>startDate</c>/<c>endDate</c> defaults to the current calendar month.
        /// The computed <c>balance</c> field equals <c>income − expenses − wages</c>.
        /// </remarks>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="divisionId">Filter by division ID. Optional.</param>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? divisionId = null)
        {
            var summary = await _service.GetSummaryAsync(startDate, endDate, divisionId);
            return Ok(summary);
        }

        /// <summary>Get monthly income, expense, and wage totals for charting.</summary>
        /// <remarks>Returns one data point per calendar month, oldest first.</remarks>
        /// <param name="months">Number of months to include (default 6).</param>
        /// <param name="divisionId">Filter by division ID. Optional.</param>
        [HttpGet("trend")]
        [ProducesResponseType(typeof(IEnumerable<TrendDataPoint>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTrend(
            [FromQuery] int months = 6,
            [FromQuery] string? divisionId = null)
        {
            var result = await _service.GetTrendAsync(months, divisionId);
            return Ok(result);
        }
    }
}
