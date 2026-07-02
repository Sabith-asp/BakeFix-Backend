using BakeFix.DTOs;
using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Wage/payroll records. Requires the <b>Wages</b> module to be enabled.</summary>
    [ApiController]
    [Route("wage")]
    [Authorize]
    [RequireModule("Wages")]
    [Produces("application/json")]
    public class WageController : ControllerBase
    {
        private readonly WageService _service;

        public WageController(WageService service)
        {
            _service = service;
        }

        /// <summary>List wage records with optional date, employee, division, and pagination filters.</summary>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Records per page (default 20).</param>
        /// <param name="employeeId">Filter by employee ID. Optional.</param>
        /// <param name="divisionId">Filter by division ID. Optional.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Wage>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>Get total wages paid per employee in a date range.</summary>
        /// <remarks>Useful for a payroll summary view — returns one row per employee.</remarks>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        [HttpGet("employee-summary")]
        [ProducesResponseType(typeof(IEnumerable<EmployeeWageSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetEmployeeSummary(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            var result = await _service.GetEmployeeSummaryAsync(startDate, endDate);
            return Ok(result);
        }

        /// <summary>Record a new wage payment.</summary>
        /// <param name="request">Wage payment details including employee and amount.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Wage), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>Update an existing wage record.</summary>
        /// <param name="id">Wage record ID.</param>
        /// <param name="request">Updated wage details.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        /// <summary>Delete a wage record.</summary>
        /// <param name="id">Wage record ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Wage not found" });

            return NoContent();
        }
    }
}
