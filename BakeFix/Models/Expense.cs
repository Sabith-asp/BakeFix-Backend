namespace BakeFix.Models
{
    public class Expense
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class ExpenseFormData
    {
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public DateTime Date { get; set; }
        public string? PaymentMethod { get; set; }
    }
}
