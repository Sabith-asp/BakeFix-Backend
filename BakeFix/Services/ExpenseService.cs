using BakeFix.DTOs;
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

        public async Task<PagedResult<Expense>> GetAllAsync(string? startDate, string? endDate, int page, int pageSize, string? category = null, string? divisionId = null)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int safePage = Math.Max(1, page);
            int safePageSize = Math.Clamp(pageSize, 1, 100);

            var (items, totalCount, totalAmount) = await _repo.GetAllAsync(s, e, safePage, safePageSize, category, divisionId);

            return new PagedResult<Expense>
            {
                Items = items,
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                Page = safePage,
                PageSize = safePageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public async Task<Expense> CreateAsync(ExpenseFormData request)
        {
            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Description = request.Description,
                Category = request.Category,
                PaymentMethod = request.PaymentMethod,
                DivisionId = request.DivisionId,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.CreateAsync(expense);
        }

        public async Task<bool> UpdateAsync(string id, ExpenseFormData request)
        {
            var expense = new Expense
            {
                Id = Guid.Parse(id),
                Amount = request.Amount,
                Description = request.Description,
                Category = request.Category,
                PaymentMethod = request.PaymentMethod,
                DivisionId = request.DivisionId,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.UpdateAsync(expense);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
