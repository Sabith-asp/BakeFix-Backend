using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("debts")]
    [Authorize]
    [RequireModule("Debts")]
    public class DebtController : ControllerBase
    {
        private readonly DebtService _service;

        public DebtController(DebtService service)
        {
            _service = service;
        }

        // GET /debts?type=Payable&settled=false
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? type = null,
            [FromQuery] bool? settled = null)
        {
            var debts = await _service.GetAllAsync(type, settled);
            return Ok(debts);
        }

        // GET /debts/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var debt = await _service.GetByIdAsync(id);
            if (debt is null) return NotFound(new { message = "Debt not found" });
            return Ok(debt);
        }

        // POST /debts
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DebtFormData request)
        {
            if (string.IsNullOrWhiteSpace(request.PersonName))
                return BadRequest(new { message = "Person name is required" });
            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than zero" });
            if (request.Type != "Payable" && request.Type != "Receivable")
                return BadRequest(new { message = "Type must be Payable or Receivable" });

            var debt = await _service.CreateAsync(request);
            return Ok(debt);
        }

        // PUT /debts/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] DebtFormData request)
        {
            if (string.IsNullOrWhiteSpace(request.PersonName))
                return BadRequest(new { message = "Person name is required" });
            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than zero" });
            if (request.Type != "Payable" && request.Type != "Receivable")
                return BadRequest(new { message = "Type must be Payable or Receivable" });

            var success = await _service.UpdateAsync(id, request);
            if (!success) return NotFound(new { message = "Debt not found" });
            return NoContent();
        }

        // DELETE /debts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(new { message = "Debt not found" });
            return NoContent();
        }

        // POST /debts/{id}/payments
        [HttpPost("{id}/payments")]
        public async Task<IActionResult> AddPayment(string id, [FromBody] DebtPaymentFormData request)
        {
            if (request.Amount <= 0)
                return BadRequest(new { message = "Payment amount must be greater than zero" });

            try
            {
                var payment = await _service.AddPaymentAsync(id, request);
                return Ok(payment);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // DELETE /debts/{id}/payments/{paymentId}
        [HttpDelete("{id}/payments/{paymentId}")]
        public async Task<IActionResult> DeletePayment(string id, string paymentId)
        {
            var success = await _service.DeletePaymentAsync(id, paymentId);
            if (!success) return NotFound(new { message = "Payment not found" });
            return NoContent();
        }

        // PATCH /debts/{id}/settle
        [HttpPatch("{id}/settle")]
        public async Task<IActionResult> Settle(string id)
        {
            var success = await _service.SettleAsync(id);
            if (!success) return NotFound(new { message = "Debt not found" });
            return NoContent();
        }
    }
}
