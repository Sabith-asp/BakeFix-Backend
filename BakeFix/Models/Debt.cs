namespace BakeFix.Models
{
    public class Debt
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string PersonName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "Payable" | "Receivable"
        public DateTime Date { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }
        public bool IsSettled { get; set; }
        public DateTime CreatedAt { get; set; }

        // Aggregated from DebtPayments (set by repository)
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount => Amount - PaidAmount;

        // Only populated by GetByIdAsync
        public List<DebtPayment>? Payments { get; set; }
    }

    public class DebtPayment
    {
        public Guid Id { get; set; }
        public Guid DebtId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DebtFormData
    {
        public string PersonName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }
    }

    public class DebtPaymentFormData
    {
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }
}
