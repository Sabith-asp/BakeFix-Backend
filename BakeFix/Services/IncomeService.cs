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

        public async Task<IEnumerable<Income>> GetAllAsync(string? startDate, string? endDate, int? limit)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int? safeLimit = limit.HasValue ? Math.Clamp(limit.Value, 1, 500) : null;

            return await _repo.GetAllAsync(s, e, safeLimit);
        }

        public async Task<Income> CreateAsync(IncomeFormData request)
        {
            var income = new Income
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Description = request.Description,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.CreateAsync(income);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
