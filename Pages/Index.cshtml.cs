using System.Globalization;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages;

public class IndexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public decimal SalesToday { get; private set; }
    public decimal ExpensesToday { get; private set; }
    public int LowStockProducts { get; private set; }
    public int SalesCountToday { get; private set; }
    public decimal NetToday => SalesToday - ExpensesToday;

    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public List<Sale> RecentSales { get; private set; } = new();
    public List<ProductStock> LowStockList { get; private set; } = new();
    public List<ProductRankingItem> TopProducts { get; private set; } = new();
    public List<string> WeekLabels { get; private set; } = new();
    public List<decimal> WeekSales { get; private set; } = new();
    public List<decimal> WeekCosts { get; private set; } = new();
    public List<string> PaymentLabels { get; private set; } = new();
    public List<decimal> PaymentTotals { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var today = DateTime.UtcNow.Date;
        SalesToday = await db.Sales.Where(x => x.Date >= today && x.Status == "Completed" && x.BranchId == BranchId).SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        ExpensesToday = await db.Expenses.Where(x => x.Date >= today).SumAsync(x => (decimal?)x.Amount) ?? 0m;
        SalesCountToday = await db.Sales.CountAsync(x => x.Date >= today && x.Status == "Completed" && x.BranchId == BranchId);
        LowStockProducts = await db.ProductStocks.CountAsync(x => x.BranchId == BranchId && x.Stock <= x.MinStock);

        RecentSales = await db.Sales
            .Include(x => x.Customer)
            .Where(x => x.BranchId == BranchId)
            .OrderByDescending(x => x.Date)
            .Take(6)
            .ToListAsync();

        LowStockList = await db.ProductStocks
            .Include(x => x.Product)!.ThenInclude(p => p!.Category)
            .Where(x => x.BranchId == BranchId && x.Stock <= (x.MinStock + 3))
            .OrderBy(x => x.Stock)
            .Take(8)
            .ToListAsync();

        TopProducts = await db.SaleDetails
            .Include(x => x.Product)
            .Include(x => x.Sale)
            .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed")
            .GroupBy(x => new { x.ProductId, Name = x.Product != null ? x.Product.Name : "Producto" })
            .Select(g => new ProductRankingItem
            {
                ProductName = g.Key.Name,
                Units = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.Units)
            .Take(6)
            .ToListAsync();

        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-6);

        var salesWindow = await db.Sales
            .Where(s => s.BranchId == BranchId && s.Status == "Completed" && s.Date >= startDate)
            .Select(s => new { s.Date, s.TotalAmount })
            .ToListAsync();

        var costWindow = await db.Expenses
            .Where(e => e.Date >= startDate)
            .Select(e => new { e.Date, e.Amount })
            .ToListAsync();

        WeekLabels = Enumerable.Range(0, 7)
            .Select(i => startDate.AddDays(i).ToString("ddd", new CultureInfo("es-MX")))
            .ToList();

        WeekSales = Enumerable.Range(0, 7)
            .Select(i => salesWindow.Where(s => s.Date.Date == startDate.AddDays(i)).Sum(s => s.TotalAmount))
            .ToList();

        WeekCosts = Enumerable.Range(0, 7)
            .Select(i => costWindow.Where(e => e.Date.Date == startDate.AddDays(i)).Sum(e => e.Amount))
            .ToList();

        var paymentMix = await db.Sales
            .Where(s => s.BranchId == BranchId && s.Status == "Completed" && s.Date >= startDate)
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new { Payment = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        PaymentLabels = paymentMix
            .Select(x => x.Payment switch
            {
                "Cash" => "Efectivo",
                "Card" => "Tarjeta",
                "Transfer" => "Transferencia",
                _ => x.Payment
            })
            .ToList();

        PaymentTotals = paymentMix.Select(x => x.Total).ToList();
    }

    public class ProductRankingItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal Amount { get; set; }
    }
}
