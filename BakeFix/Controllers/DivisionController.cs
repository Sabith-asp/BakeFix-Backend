using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Division management. Requires the <b>Divisions</b> module to be enabled.</summary>
    /// <remarks>
    /// Divisions allow grouping of income, expenses, and wages by business unit
    /// (e.g. individual bakery outlets or product lines).
    /// </remarks>
    [ApiController]
    [Route("division")]
    [Authorize]
    [RequireModule("Divisions")]
    [Produces("application/json")]
    public class DivisionController : ControllerBase
    {
        private readonly DivisionService _service;

        public DivisionController(DivisionService service)
        {
            _service = service;
        }

        /// <summary>List all divisions in the organisation.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Division>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var divisions = await _service.GetAllAsync();
            return Ok(divisions);
        }

        /// <summary>Create a new division.</summary>
        /// <param name="request">Division name.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Division), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] DivisionFormData request)
        {
            try
            {
                var division = await _service.CreateAsync(request);
                return Ok(division);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Rename a division.</summary>
        /// <param name="id">Division ID.</param>
        /// <param name="request">New division name.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(string id, [FromBody] DivisionFormData request)
        {
            try
            {
                var success = await _service.UpdateAsync(id, request);

                if (!success)
                    return NotFound(new { message = "Division not found" });

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Delete a division.</summary>
        /// <remarks>Will return <c>400</c> if the division still has linked records.</remarks>
        /// <param name="id">Division ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var success = await _service.DeleteAsync(id);

                if (!success)
                    return NotFound(new { message = "Division not found" });

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
