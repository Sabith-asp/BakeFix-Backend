namespace BakeFix.Models
{
    public class InventorySummary
    {
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public decimal TodaySalesAmount { get; set; }
        public int TodaySalesCount { get; set; }
    }
}
