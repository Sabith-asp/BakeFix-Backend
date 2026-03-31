namespace BakeFix.Models
{
    public class EmployeeWageSummary
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
