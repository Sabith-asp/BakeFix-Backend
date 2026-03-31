using BakeFix.DTOs;
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

        public async Task<PagedResult<Wage>> GetAllAsync(string? startDate, string? endDate, int page, int pageSize, string? employeeId = null)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            int safePage = Math.Max(1, page);
            int safePageSize = Math.Clamp(pageSize, 1, 100);

            var (items, totalCount, totalAmount) = await _repo.GetAllAsync(s, e, safePage, safePageSize, employeeId);

            return new PagedResult<Wage>
            {
                Items = items,
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                Page = safePage,
                PageSize = safePageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public async Task<IEnumerable<EmployeeWageSummary>> GetEmployeeSummaryAsync(string? startDate, string? endDate)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
            return await _repo.GetEmployeeSummaryAsync(s, e);
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

        public async Task<bool> UpdateAsync(string id, WageFormData request)
        {
            var employeeExists = await _employeeRepo.ExistsAsync(request.EmployeeId);
            if (!employeeExists)
            {
                throw new ArgumentException("Selected employee does not exist");
            }

            var wage = new Wage
            {
                Id = Guid.Parse(id),
                Amount = request.Amount,
                EmployeeId = request.EmployeeId,
                Description = request.Description,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _repo.UpdateAsync(wage);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}
