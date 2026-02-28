using BakeFix.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class EmployeeRepository
    {
        private readonly string _conn;

        public EmployeeRepository(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Employee>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"SELECT Id, Name, CreatedAt
                                   FROM Employees
                                   ORDER BY Name ASC";

            return await connection.QueryAsync<Employee>(query);
        }

        public async Task<Employee> CreateAsync(Employee employee)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = @"INSERT INTO Employees (Id, Name, CreatedAt)
                                   VALUES (@Id, @Name, @CreatedAt);";

            await connection.ExecuteAsync(query, employee);
            return employee;
        }

        public async Task<bool> ExistsAsync(Guid employeeId)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = "SELECT COUNT(1) FROM Employees WHERE Id = @employeeId";
            var count = await connection.ExecuteScalarAsync<int>(query, new { employeeId });
            return count > 0;
        }

        public async Task<bool> NameExistsAsync(string name)
        {
            using var connection = new MySqlConnection(_conn);

            const string query = "SELECT COUNT(1) FROM Employees WHERE LOWER(Name) = LOWER(@name)";
            var count = await connection.ExecuteScalarAsync<int>(query, new { name });
            return count > 0;
        }
    }
}
