using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Sale> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await db.Sales
            .Include(x => x.User)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.Date)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason)
    {
        var sale = await db.Sales
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale is null)
        {
            return RedirectToPage();
        }

        if (!string.Equals(sale.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash"] = "Solo se pueden cancelar ventas completadas.";
            return RedirectToPage();
        }

        var productIds = sale.Details.Select(d => d.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        var map = products.ToDictionary(p => p.Id);

        foreach (var detail in sale.Details)
        {
            if (map.TryGetValue(detail.ProductId, out var product))
            {
                product.Stock += detail.Quantity;
            }
        }

        sale.Status = "Cancelled";
        sale.CancelledAt = DateTime.UtcNow;
        sale.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelada desde historial" : reason.Trim();

        await db.SaveChangesAsync();
        TempData["Flash"] = $"Venta #{sale.Id} cancelada y stock restaurado.";
        return RedirectToPage();
    }
}
