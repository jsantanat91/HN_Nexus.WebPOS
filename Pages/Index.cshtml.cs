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
    public decimal AvgTicketToday { get; private set; }
    public decimal MarginToday { get; private set; }
    public decimal NetToday => SalesToday - ExpensesToday;

    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Historical { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = DateTime.Today;

    public List<Sale> RecentSales { get; private set; } = new();
    public List<ProductStock> LowStockList { get; private set; } = new();
    public List<ProductRankingItem> TopProducts { get; private set; } = new();
    public List<string> WeekLabels { get; private set; } = new();
    public List<decimal> WeekSales { get; private set; } = new();
    public List<decimal> WeekCosts { get; private set; } = new();
    public List<string> PaymentLabels { get; private set; } = new();
    public List<decimal> PaymentTotals { get; private set; } = new();
    public List<CashierRankingItem> TopCashiers { get; private set; } = new();
    public List<CategoryRankingItem> TopCategories { get; private set; } = new();
    public List<ForecastRiskItem> ForecastRisks { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        if (!Historical)
        {
            var nowLocal = DateTime.Now;
            From = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            To = nowLocal.Date;
        }

        var periodFromUtc = DateTime.SpecifyKind(From.Date, DateTimeKind.Utc);
        var periodToUtc = DateTime.SpecifyKind(To.Date.AddDays(1), DateTimeKind.Utc);

        var salesQuery = db.Sales.Where(x => x.Status == "Completed" && x.BranchId == BranchId && x.Date >= periodFromUtc && x.Date < periodToUtc);
        var expensesQuery = db.Expenses.Where(x => x.Date >= periodFromUtc && x.Date < periodToUtc);

        if (!Historical)
        {
            var factRows = await db.FactDailyBranches
                .Where(x => x.BranchId == BranchId && x.PeriodDate >= From.Date && x.PeriodDate <= To.Date)
                .ToListAsync();

            if (factRows.Count > 0)
            {
                SalesToday = factRows.Sum(x => x.SalesTotal);
                ExpensesToday = factRows.Sum(x => x.ExpenseTotal);
                SalesCountToday = (int)factRows.Sum(x => x.SalesCount);
                MarginToday = factRows.Sum(x => x.SalesTotal - x.CostTotal);
            }
            else
            {
                SalesToday = await salesQuery.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
                ExpensesToday = await expensesQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;
                SalesCountToday = await salesQuery.CountAsync();
                MarginToday = await db.SaleDetails
                    .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed" && x.Sale.Date >= periodFromUtc && x.Sale.Date < periodToUtc)
                    .SumAsync(x => (decimal?)(x.Quantity * (x.UnitPrice - (x.Product != null ? x.Product.Cost : 0m)))) ?? 0m;
            }
        }
        else
        {
            SalesToday = await salesQuery.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
            ExpensesToday = await expensesQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;
            SalesCountToday = await salesQuery.CountAsync();
            MarginToday = await db.SaleDetails
                .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed" && x.Sale.Date >= periodFromUtc && x.Sale.Date < periodToUtc)
                .SumAsync(x => (decimal?)(x.Quantity * (x.UnitPrice - (x.Product != null ? x.Product.Cost : 0m)))) ?? 0m;
        }

        AvgTicketToday = SalesCountToday > 0 ? SalesToday / SalesCountToday : 0m;
        LowStockProducts = await db.ProductStocks.CountAsync(x => x.BranchId == BranchId && x.Stock <= x.MinStock);

        RecentSales = await db.Sales
            .Include(x => x.Customer)
            .Where(x => x.BranchId == BranchId && x.Date >= periodFromUtc && x.Date < periodToUtc)
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
            .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed" && x.Sale.Date >= periodFromUtc && x.Sale.Date < periodToUtc)
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

        var salesWindow = await db.Sales
            .Where(s => s.BranchId == BranchId && s.Status == "Completed" && s.Date >= periodFromUtc && s.Date < periodToUtc)
            .Select(s => new { s.Date, s.TotalAmount, s.PaymentMethod })
            .ToListAsync();

        var costWindow = await db.Expenses
            .Where(e => e.Date >= periodFromUtc && e.Date < periodToUtc)
            .Select(e => new { e.Date, e.Amount })
            .ToListAsync();

        var days = (To.Date - From.Date).Days + 1;
        if (days < 1) days = 1;
        if (days > 62) days = 62;

        WeekLabels = Enumerable.Range(0, days)
            .Select(i => From.Date.AddDays(i).ToString("dd MMM", new CultureInfo("es-MX")))
            .ToList();

        WeekSales = Enumerable.Range(0, days)
            .Select(i => salesWindow.Where(s => s.Date.Date == From.Date.AddDays(i)).Sum(s => s.TotalAmount))
            .ToList();

        WeekCosts = Enumerable.Range(0, days)
            .Select(i => costWindow.Where(e => e.Date.Date == From.Date.AddDays(i)).Sum(e => e.Amount))
            .ToList();

        var paymentMix = salesWindow
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new { Payment = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToList();

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

        TopCashiers = await db.Sales
            .Include(x => x.User)
            .Where(x => x.BranchId == BranchId && x.Status == "Completed" && x.Date >= periodFromUtc && x.Date < periodToUtc)
            .GroupBy(x => new { x.UserId, Name = x.User != null ? x.User.FullName : "N/A" })
            .Select(g => new CashierRankingItem
            {
                Cashier = g.Key.Name,
                Tickets = g.Count(),
                Amount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Amount)
            .Take(6)
            .ToListAsync();

        TopCategories = await db.SaleDetails
            .Include(x => x.Sale)
            .Include(x => x.Product)!.ThenInclude(p => p!.Category)
            .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed" && x.Sale.Date >= periodFromUtc && x.Sale.Date < periodToUtc)
            .GroupBy(x => new { Name = x.Product != null && x.Product.Category != null ? x.Product.Category.Name : "General" })
            .Select(g => new CategoryRankingItem
            {
                Category = g.Key.Name,
                Units = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.Amount)
            .Take(6)
            .ToListAsync();

        var forecastStart = DateTime.UtcNow.Date.AddDays(-30);
        var sales30 = await db.SaleDetails
            .Include(x => x.Sale)
            .Where(x => x.Sale != null && x.Sale.BranchId == BranchId && x.Sale.Status == "Completed" && x.Sale.Date >= forecastStart)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var stockRows = await db.ProductStocks
            .Include(x => x.Product)
            .Where(x => x.BranchId == BranchId)
            .ToListAsync();

        ForecastRisks = stockRows
            .Select(s =>
            {
                var sold30 = sales30.FirstOrDefault(x => x.ProductId == s.ProductId)?.Units ?? 0;
                var dailyAvg = sold30 / 30m;
                var daysCover = dailyAvg > 0 ? s.Stock / dailyAvg : 999m;
                return new ForecastRiskItem
                {
                    ProductName = s.Product?.Name ?? "Producto",
                    CurrentStock = s.Stock,
                    DailyAvg = dailyAvg,
                    DaysCover = daysCover
                };
            })
            .OrderBy(x => x.DaysCover)
            .Take(8)
            .ToList();
    }

    public class ProductRankingItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal Amount { get; set; }
    }

    public class CashierRankingItem
    {
        public string Cashier { get; set; } = string.Empty;
        public int Tickets { get; set; }
        public decimal Amount { get; set; }
    }

    public class CategoryRankingItem
    {
        public string Category { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal Amount { get; set; }
    }

    public class ForecastRiskItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public decimal DailyAvg { get; set; }
        public decimal DaysCover { get; set; }
    }
}
