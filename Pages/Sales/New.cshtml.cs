using System.Globalization;
using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class NewModel(AppDbContext db) : PageModel
{
    public List<Product> Products { get; private set; } = new();
    public List<SelectListItem> Customers { get; private set; } = new();

    [BindProperty]
    public int? CustomerId { get; set; }

    [BindProperty]
    public bool IsInvoice { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        var selected = new List<(Product Product, int Qty)>();
        foreach (var product in Products)
        {
            var key = $"qty_{product.Id}";
            if (!Request.Form.TryGetValue(key, out var qtyRaw))
            {
                continue;
            }

            if (!int.TryParse(qtyRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                continue;
            }

            if (product.Stock < qty)
            {
                ModelState.AddModelError(string.Empty, $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}.");
                return Page();
            }

            selected.Add((product, qty));
        }

        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debes capturar al menos un producto con cantidad > 0.");
            return Page();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var sale = new Sale
        {
            Date = DateTime.UtcNow,
            UserId = userId,
            CustomerId = CustomerId,
            IsInvoice = IsInvoice,
            Status = "Completed"
        };

        foreach (var line in selected)
        {
            sale.Details.Add(new SaleDetail
            {
                ProductId = line.Product.Id,
                Quantity = line.Qty,
                UnitPrice = line.Product.Price
            });

            line.Product.Stock -= line.Qty;
        }

        sale.TotalAmount = sale.Details.Sum(x => x.Total);

        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Venta #{sale.Id} registrada.";
        return RedirectToPage("/Sales/Ticket", new { id = sale.Id });
    }

    private async Task LoadAsync()
    {
        Products = await db.Products.Include(x => x.Category).OrderBy(x => x.Name).ToListAsync();
        Customers = await db.Customers.Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();
    }
}
