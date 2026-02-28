using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class ExpenseService
    {
        private readonly ExpenseRepository _repo;

        public ExpenseService(ExpenseRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Expense>> GetAllAsync(string? startDate, string? endDate, int? limit)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int? safeLimit = limit.HasValue ? Math.Clamp(limit.Value, 1, 500) : null;

            return await _repo.GetAllAsync(s, e, safeLimit);
        }

        public async Task<Expense> CreateAsync(ExpenseFormData request)
        {
            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Description = request.Description,
                Category = request.Category,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.CreateAsync(expense);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
