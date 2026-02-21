using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class WageService
    {
        private readonly WageRepository _repo;

        public WageService(WageRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Wage>> GetAllAsync(string? startDate, string? endDate)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

            return await _repo.GetAllAsync(s, e);
        }

        public async Task<Wage> CreateAsync(WageFormData request)
        {
            var wage = new Wage
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                EmployeeName = request.EmployeeName,
                Description = request.Description,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.CreateAsync(wage);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
