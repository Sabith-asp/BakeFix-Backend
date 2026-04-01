using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("employee")]
    [Authorize]
    [RequireModule("Employees")]
    public class EmployeeController : ControllerBase
    {
        private readonly EmployeeService _service;

        public EmployeeController(EmployeeService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _service.GetAllAsync();
            return Ok(employees);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] EmployeeFormData request)
        {
            try
            {
                var success = await _service.UpdateAsync(id, request);

                if (!success)
                    return NotFound(new { message = "Employee not found" });

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
                    return NotFound(new { message = "Employee not found" });

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EmployeeFormData request)
        {
            try
            {
                var employee = await _service.CreateAsync(request);
                return Ok(employee);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
