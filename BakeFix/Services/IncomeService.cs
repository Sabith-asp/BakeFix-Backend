using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class IncomeService
    {
        private readonly IncomeRepository _repo;

        public IncomeService(IncomeRepository repo)
        {
            _repo = repo;
        }

        public async Task<PagedResult<Income>> GetAllAsync(string? startDate, string? endDate, int page, int pageSize)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int safePage = Math.Max(1, page);
            int safePageSize = Math.Clamp(pageSize, 1, 100);

            var (items, totalCount, totalAmount) = await _repo.GetAllAsync(s, e, safePage, safePageSize);

            return new PagedResult<Income>
            {
                Items = items,
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                Page = safePage,
                PageSize = safePageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public async Task<Income> CreateAsync(IncomeFormData request)
        {
            var income = new Income
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Description = request.Description,
                PaymentMethod = request.PaymentMethod,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.CreateAsync(income);
        }

        public async Task<bool> UpdateAsync(string id, IncomeFormData request)
        {
            var income = new Income
            {
                Id = Guid.Parse(id),
                Amount = request.Amount,
                Description = request.Description,
                PaymentMethod = request.PaymentMethod,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.UpdateAsync(income);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
