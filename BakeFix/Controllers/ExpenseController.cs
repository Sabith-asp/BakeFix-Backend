using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Expense records for the caller's organisation.</summary>
    [ApiController]
    [Route("expense")]
    [Authorize]
    [Produces("application/json")]
    public class ExpenseController : ControllerBase
    {
        private readonly ExpenseService _service;

        public ExpenseController(ExpenseService service)
        {
            _service = service;
        }

        /// <summary>List expense records with optional date, category, division, and pagination filters.</summary>
        /// <param name="startDate">Start of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="endDate">End of date range (yyyy-MM-dd). Optional.</param>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Records per page (default 20).</param>
        /// <param name="category">Filter by expense category name. Optional.</param>
        /// <param name="divisionId">Filter by division ID. Optional.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Expense>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? category = null,
            [FromQuery] string? divisionId = null)
        {
            var result = await _service.GetAllAsync(startDate, endDate, page, pageSize, category, divisionId);
            return Ok(result);
        }

        /// <summary>Record a new expense entry.</summary>
        /// <param name="request">Expense details.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] ExpenseFormData request)
        {
            var expense = await _service.CreateAsync(request);
            return Ok(expense);
        }

        /// <summary>Update an existing expense record.</summary>
        /// <param name="id">Expense record ID.</param>
        /// <param name="request">Updated expense details.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(string id, [FromBody] ExpenseFormData request)
        {
            var success = await _service.UpdateAsync(id, request);

            if (!success)
                return NotFound(new { message = "Expense not found" });

            return NoContent();
        }

        /// <summary>Delete an expense record.</summary>
        /// <param name="id">Expense record ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new { message = "Expense not found" });

            return NoContent();
        }
    }
}
