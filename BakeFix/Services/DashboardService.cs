using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class DashboardService
    {
        private readonly DashboardRepository _repo;

        public DashboardService(DashboardRepository repo)
        {
            _repo = repo;
        }

        public async Task<DashboardSummary> GetSummaryAsync(string? startDate, string? endDate)
        {
            DateTime? s = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? e = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

            return await _repo.GetSummaryAsync(s, e);
        }
    }
}
