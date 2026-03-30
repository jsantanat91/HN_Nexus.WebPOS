using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.SupplierOrders;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<SupplierOrder> Items { get; private set; } = new();
    public List<SelectListItem> Suppliers { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(int supplierId, int productId, int quantity, decimal unitCost)
    {
        if (supplierId <= 0 || productId <= 0 || quantity <= 0)
        {
            TempData["Flash"] = "Captura datos válidos para el pedido.";
            return RedirectToPage();
        }

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);

        if (supplier is null || product is null)
        {
            TempData["Flash"] = "Proveedor o producto inválido.";
            return RedirectToPage();
        }

        db.SupplierOrders.Add(new SupplierOrder
        {
            SupplierId = supplierId,
            ProductId = productId,
            Quantity = quantity,
            UnitCost = unitCost < 0 ? 0 : unitCost,
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

        var stockRows = await db.ProductStocks.Where(x => x.ProductId == order.ProductId).ToListAsync();
        foreach (var row in stockRows)
        {
            row.Stock += order.Quantity;
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == order.ProductId);
        if (product is not null)
        {
            product.Stock += order.Quantity;
            if (order.UnitCost > 0)
            {
                product.Cost = order.UnitCost;
            }
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Pedido marcado como recibido.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Suppliers = await db.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();

        Products = await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem($"{p.Name} (Stock {p.Stock})", p.Id.ToString()))
            .ToListAsync();

        Items = await db.SupplierOrders
            .Include(x => x.Supplier)
            .Include(x => x.Product)
            .OrderByDescending(x => x.OrderDate)
            .Take(200)
            .ToListAsync();
    }
}

