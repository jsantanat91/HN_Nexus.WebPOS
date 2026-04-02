using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Reports;

[Authorize(Policy = "AdminOnly")]
public class EnterpriseModel(AppDbContext db, ITenantAnalyticsService tenantAnalytics) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    public List<TenantSalesMetric> TenantMetrics { get; private set; } = new();
    public List<BranchMetric> BranchRanking { get; private set; } = new();
    public List<CashierMetric> CashierRanking { get; private set; } = new();
    public List<CategoryMarginMetric> CategoryMargins { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Days = Math.Clamp(Days, 1, 120);
        var from = DateTime.UtcNow.AddDays(-Days);
        var to = DateTime.UtcNow;

        TenantMetrics = await tenantAnalytics.GetTenantSalesMetricsAsync(from, to);

        BranchRanking = await db.Sales
            .Include(x => x.Branch)
            .Where(x => x.Status == "Completed" && x.Date >= from)
            .GroupBy(x => new { x.BranchId, Branch = x.Branch != null ? x.Branch.Name : "N/A" })
            .Select(g => new BranchMetric
            {
                Branch = g.Key.Branch,
                Tickets = g.Count(),
                Total = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Total)
            .Take(12)
            .ToListAsync();

        CashierRanking = await db.Sales
            .Include(x => x.User)
            .Where(x => x.Status == "Completed" && x.Date >= from)
            .GroupBy(x => new { x.UserId, Cashier = x.User != null ? x.User.FullName : "N/A" })
            .Select(g => new CashierMetric
            {
                Cashier = g.Key.Cashier,
                Tickets = g.Count(),
                Total = g.Sum(x => x.TotalAmount),
                AvgTicket = g.Average(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Total)
            .Take(12)
            .ToListAsync();

        CategoryMargins = await db.SaleDetails
            .Include(x => x.Sale)
            .Include(x => x.Product)!.ThenInclude(p => p!.Category)
            .Where(x => x.Sale != null && x.Sale.Status == "Completed" && x.Sale.Date >= from)
            .GroupBy(x => x.Product != null && x.Product.Category != null ? x.Product.Category.Name : "General")
            .Select(g => new CategoryMarginMetric
            {
                Category = g.Key,
                Sales = g.Sum(x => x.Quantity * x.UnitPrice),
                Cost = g.Sum(x => x.Quantity * (x.Product != null ? x.Product.Cost : 0m))
            })
            .OrderByDescending(x => x.Margin)
            .Take(12)
            .ToListAsync();
    }

    public class BranchMetric
    {
        public string Branch { get; set; } = string.Empty;
        public int Tickets { get; set; }
        public decimal Total { get; set; }
    }

    public class CashierMetric
    {
        public string Cashier { get; set; } = string.Empty;
        public int Tickets { get; set; }
        public decimal Total { get; set; }
        public decimal AvgTicket { get; set; }
    }

    public class CategoryMarginMetric
    {
        public string Category { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public decimal Cost { get; set; }
        public decimal Margin => Sales - Cost;
    }
}
