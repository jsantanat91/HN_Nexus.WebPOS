using HN_Nexus.WebPOS.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public decimal SalesToday { get; private set; }
    public decimal ExpensesToday { get; private set; }
    public int LowStockProducts { get; private set; }
    public int SalesCountToday { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        SalesToday = await db.Sales.Where(x => x.Date >= today).SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        ExpensesToday = await db.Expenses.Where(x => x.Date >= today).SumAsync(x => (decimal?)x.Amount) ?? 0m;
        SalesCountToday = await db.Sales.CountAsync(x => x.Date >= today);
        LowStockProducts = await db.Products.CountAsync(x => x.Stock <= 5);
    }
}
