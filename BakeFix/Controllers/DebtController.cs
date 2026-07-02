using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Debt and receivable tracking. Requires the <b>Debts</b> module to be enabled.</summary>
    /// <remarks>
    /// Supports two debt types: <c>Payable</c> (money owed by the bakery) and
    /// <c>Receivable</c> (money owed to the bakery). Partial payments can be recorded
    /// against each debt until it is fully settled.
    /// </remarks>
    [ApiController]
    [Route("debts")]
    [Authorize]
    [RequireModule("Debts")]
    [Produces("application/json")]
    public class DebtController : ControllerBase
    {
        private readonly DebtService _service;

        public DebtController(DebtService service)
        {
            _service = service;
        }

        /// <summary>List debts with optional type and settled filters.</summary>
        /// <param name="type">Filter by type: <c>Payable</c> or <c>Receivable</c>. Optional.</param>
        /// <param name="settled">Filter by settlement status. Optional.</param>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Debt>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? type = null,
            [FromQuery] bool? settled = null)
        {
            var debts = await _service.GetAllAsync(type, settled);
            return Ok(debts);
        }

        /// <summary>Get a single debt with its full payment history.</summary>
        /// <param name="id">Debt ID.</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Debt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(string id)
        {
            var debt = await _service.GetByIdAsync(id);
            if (debt is null) return NotFound(new { message = "Debt not found" });
            return Ok(debt);
        }

        /// <summary>Create a new debt record.</summary>
        /// <remarks><c>Type</c> must be <c>Payable</c> or <c>Receivable</c>.</remarks>
        /// <param name="request">Debt details.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Debt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>Update an existing debt record.</summary>
        /// <param name="id">Debt ID.</param>
        /// <param name="request">Updated debt details.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        /// <summary>Delete a debt record.</summary>
        /// <param name="id">Debt ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(new { message = "Debt not found" });
            return NoContent();
        }

        /// <summary>Record a partial or full payment against a debt.</summary>
        /// <remarks>
        /// The debt's <c>paidAmount</c> and <c>outstandingAmount</c> are automatically
        /// recalculated from all payment records.
        /// </remarks>
        /// <param name="id">Debt ID.</param>
        /// <param name="request">Payment amount and date.</param>
        [HttpPost("{id}/payments")]
        [ProducesResponseType(typeof(DebtPayment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        /// <summary>Delete a payment record from a debt.</summary>
        /// <param name="id">Debt ID.</param>
        /// <param name="paymentId">Payment record ID.</param>
        [HttpDelete("{id}/payments/{paymentId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePayment(string id, string paymentId)
        {
            var success = await _service.DeletePaymentAsync(id, paymentId);
            if (!success) return NotFound(new { message = "Payment not found" });
            return NoContent();
        }

        /// <summary>Mark a debt as fully settled.</summary>
        /// <param name="id">Debt ID.</param>
        [HttpPatch("{id}/settle")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Settle(string id)
        {
            var success = await _service.SettleAsync(id);
            if (!success) return NotFound(new { message = "Debt not found" });
            return NoContent();
        }
    }
}
