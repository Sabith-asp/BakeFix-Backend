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
