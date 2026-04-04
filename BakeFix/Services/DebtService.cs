using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class DebtService
    {
        private readonly DebtRepository _repo;

        public DebtService(DebtRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Debt>> GetAllAsync(string? type, bool? settled)
        {
            return await _repo.GetAllAsync(type, settled);
        }

        public async Task<Debt?> GetByIdAsync(string id)
        {
            return await _repo.GetByIdAsync(id);
        }

        public async Task<Debt> CreateAsync(DebtFormData request)
        {
            var debt = new Debt
            {
                Id = Guid.NewGuid(),
                PersonName = request.PersonName.Trim(),
                Amount = request.Amount,
                Type = request.Type,
                Date = request.Date,
                DueDate = request.DueDate,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsSettled = false,
                CreatedAt = DateTime.UtcNow
            };
            return await _repo.CreateAsync(debt);
        }

        public async Task<bool> UpdateAsync(string id, DebtFormData request)
        {
            var debt = new Debt
            {
                Id = Guid.Parse(id),
                PersonName = request.PersonName.Trim(),
                Amount = request.Amount,
                Type = request.Type,
                Date = request.Date,
                DueDate = request.DueDate,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            return await _repo.UpdateAsync(debt);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }

        public async Task<DebtPayment> AddPaymentAsync(string debtId, DebtPaymentFormData request)
        {
            var payment = new DebtPayment
            {
                Id = Guid.NewGuid(),
                DebtId = Guid.Parse(debtId),
                Amount = request.Amount,
                Date = request.Date,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            return await _repo.AddPaymentAsync(debtId, payment);
        }

        public async Task<bool> DeletePaymentAsync(string debtId, string paymentId)
        {
            return await _repo.DeletePaymentAsync(debtId, paymentId);
        }

        public async Task<bool> SettleAsync(string id)
        {
            return await _repo.SettleAsync(id);
        }
    }
}
