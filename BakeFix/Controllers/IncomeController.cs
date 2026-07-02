using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Income records for the caller's organisation.</summary>
    [ApiController]
    [Route("income")]
    [Authorize]
    [Produces("application/json")]
    public class IncomeController : ControllerBase
    {
        private readonly IncomeService _service;

        public IncomeController(IncomeService service)
        {
            _service = service;
        }

        /// <summary>List income records with optional date, division, and pagination filters.</summary>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Records per page (default 20).</param>
        /// <param name="divisionId">Filter by division ID. Optional.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Income>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        /// <summary>Record a new income entry.</summary>
        /// <param name="request">Income details.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Income), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] IncomeFormData request)
        {
            var income = await _service.CreateAsync(request);
            return Ok(income);
        }

        /// <summary>Update an existing income record.</summary>
        /// <param name="id">Income record ID.</param>
        /// <param name="request">Updated income details.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(string id, [FromBody] IncomeFormData request)
        {
            var success = await _service.UpdateAsync(id, request);

            if (!success)
                return NotFound(new { message = "Income not found" });

            return NoContent();
        }

        /// <summary>Delete an income record.</summary>
        /// <param name="id">Income record ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Income not found" });

            return NoContent();
        }
    }
}
