using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("division")]
    [Authorize]
    [RequireModule("Divisions")]
    public class DivisionController : ControllerBase
    {
        private readonly DivisionService _service;

        public DivisionController(DivisionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var divisions = await _service.GetAllAsync();
            return Ok(divisions);
        }

        [HttpPost]
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

        [HttpPut("{id}")]
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

        [HttpDelete("{id}")]
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
