namespace BakeFix.Models
{
    public class WageFormData
    {
        public decimal Amount { get; set; }
        public Guid EmployeeId { get; set; }
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class Wage
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public decimal Amount { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PaymentMethod { get; set; }
    }
}
