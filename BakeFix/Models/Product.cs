namespace BakeFix.Models
{
    public class Product
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? SKU { get; set; }
        public string Unit { get; set; } = "pcs";
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal? LowStockAlert { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ProductFormData
    {
        public Guid? CategoryId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? SKU { get; set; }
        public string Unit { get; set; } = "pcs";
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal? LowStockAlert { get; set; }
    }

    public class SetProductStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
