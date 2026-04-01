namespace BakeFix.Models
{
    public class Employee
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class EmployeeFormData
    {
        public string Name { get; set; } = string.Empty;
    }
}
