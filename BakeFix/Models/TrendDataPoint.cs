namespace BakeFix.Models
{
    public class TrendDataPoint
    {
        public string Month { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Wage { get; set; }
    }
}
