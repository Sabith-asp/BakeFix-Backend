using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class EmployeeService
    {
        private readonly EmployeeRepository _repo;

        public EmployeeService(EmployeeRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Employee>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<bool> UpdateAsync(string id, EmployeeFormData request)
        {
            if (!Guid.TryParse(id, out var guid))
                throw new ArgumentException("Invalid employee ID");

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Employee name is required");

            var exists = await _repo.NameExistsForOtherAsync(name, guid);
            if (exists)
                throw new ArgumentException("Employee name already exists");

            return await _repo.UpdateAsync(guid, name);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                throw new ArgumentException("Invalid employee ID");

            var wageCount = await _repo.GetWageCountAsync(guid);
            if (wageCount > 0)
                throw new ArgumentException($"Cannot delete employee with {wageCount} wage record{(wageCount == 1 ? "" : "s")}. Remove their wages first.");

            return await _repo.DeleteAsync(guid);
        }

        public async Task<Employee> CreateAsync(EmployeeFormData request)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Employee name is required");
            }

            var exists = await _repo.NameExistsAsync(name);
            if (exists)
            {
                throw new ArgumentException("Employee already exists");
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
            };

            return await _repo.CreateAsync(employee);
        }
    }
}
