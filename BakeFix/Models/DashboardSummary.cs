namespace BakeFix.Models
{
    public class DashboardSummary
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal TotalWage { get; set; }
        public decimal Balance => TotalIncome - TotalExpense - TotalWage;
    }
}
