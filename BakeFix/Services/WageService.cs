using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class WageService
    {
        private readonly WageRepository _repo;
        private readonly EmployeeRepository _employeeRepo;

        public WageService(WageRepository repo, EmployeeRepository employeeRepo)
        {
            _repo = repo;
            _employeeRepo = employeeRepo;
        }

        public async Task<IEnumerable<Wage>> GetAllAsync(string? startDate, string? endDate, int? limit)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int? safeLimit = limit.HasValue ? Math.Clamp(limit.Value, 1, 500) : null;

            return await _repo.GetAllAsync(s, e, safeLimit);
        }

        public async Task<Wage> CreateAsync(WageFormData request)
        {
            var employeeExists = await _employeeRepo.ExistsAsync(request.EmployeeId);
            if (!employeeExists)
            {
                throw new ArgumentException("Selected employee does not exist");
            }

            var wage = new Wage
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                EmployeeId = request.EmployeeId,
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
