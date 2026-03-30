using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public decimal SalesToday { get; private set; }
    public decimal ExpensesToday { get; private set; }
    public int LowStockProducts { get; private set; }
    public int SalesCountToday { get; private set; }
    public decimal NetToday => SalesToday - ExpensesToday;

    public List<Sale> RecentSales { get; private set; } = new();
    public List<Product> LowStockList { get; private set; } = new();
    public List<ProductRankingItem> TopProducts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        SalesToday = await db.Sales.Where(x => x.Date >= today && x.Status == "Completed").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        ExpensesToday = await db.Expenses.Where(x => x.Date >= today).SumAsync(x => (decimal?)x.Amount) ?? 0m;
        SalesCountToday = await db.Sales.CountAsync(x => x.Date >= today && x.Status == "Completed");
        LowStockProducts = await db.Products.CountAsync(x => x.Stock <= 5);

        RecentSales = await db.Sales
            .Include(x => x.Customer)
            .OrderByDescending(x => x.Date)
            .Take(6)
            .ToListAsync();

        LowStockList = await db.Products
            .Include(x => x.Category)
            .Where(x => x.Stock <= 8)
            .OrderBy(x => x.Stock)
            .Take(8)
            .ToListAsync();

        TopProducts = await db.SaleDetails
            .Include(x => x.Product)
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
    }

    public class ProductRankingItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal Amount { get; set; }
    }
}
