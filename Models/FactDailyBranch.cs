namespace HN_Nexus.WebPOS.Models;

public class FactDailyBranch
{
    public long Id { get; set; }
    public DateOnly PeriodDate { get; set; }
    public int BranchId { get; set; }
    public decimal SalesCount { get; set; }
    public decimal SalesSubtotal { get; set; }
    public decimal SalesTax { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal CostTotal { get; set; }
    public decimal ExpenseTotal { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
