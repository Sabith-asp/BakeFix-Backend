namespace BakeFix.Models
{
    public class StockTransaction
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductUnit { get; set; }
        public string Type { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string? SupplierOrCustomer { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? DivisionId { get; set; }
        public string? DivisionName { get; set; }
        public string? Note { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StockTransactionFormData
    {
        public Guid ProductId { get; set; }
        public string Type { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? SupplierOrCustomer { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? DivisionId { get; set; }
        public string? Note { get; set; }
        public DateTime Date { get; set; }
    }
}
