using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.SupplierOrders;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<SupplierOrder> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await db.SupplierOrders.OrderByDescending(x => x.OrderDate).Take(200).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string providerName, string productName, int quantity)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(productName) || quantity <= 0)
        {
            TempData["Flash"] = "Captura datos validos para el pedido.";
            return RedirectToPage();
        }

        db.SupplierOrders.Add(new SupplierOrder
        {
            ProviderName = providerName,
            ProductName = productName,
            Quantity = quantity,
            Status = "Pendiente",
            OrderDate = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Pedido registrado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkReceivedAsync(int id)
    {
        var order = await db.SupplierOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (order is null)
        {
            return RedirectToPage();
        }

        order.Status = "Recibido";
        await db.SaveChangesAsync();
        TempData["Flash"] = "Pedido marcado como recibido.";
        return RedirectToPage();
    }
}
