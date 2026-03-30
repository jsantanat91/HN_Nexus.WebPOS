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
    public AppConfig Config { get; private set; } = new();

    [BindProperty]
    public int? CustomerId { get; set; }

    [BindProperty]
    public bool IsInvoice { get; set; }

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public decimal AmountReceived { get; set; }

    [BindProperty]
    public decimal GlobalDiscountPercent { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        var selected = new List<(Product Product, int Qty, decimal DiscountPercent)>();
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

            var discountRaw = Request.Form[$"disc_{product.Id}"].FirstOrDefault() ?? "0";
            _ = decimal.TryParse(discountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var lineDiscountPercent);
            lineDiscountPercent = Math.Clamp(lineDiscountPercent, 0m, 100m);

            if (product.Stock < qty)
            {
                ModelState.AddModelError(string.Empty, $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}.");
                return Page();
            }

            selected.Add((product, qty, lineDiscountPercent));
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

        var taxRate = Math.Clamp(Config.TaxRate, 0m, 100m);
        var subtotalGross = selected.Sum(x => x.Product.Price * x.Qty);
        var lineDiscountAmount = selected.Sum(x => (x.Product.Price * x.Qty) * (x.DiscountPercent / 100m));
        var subtotalAfterLineDiscount = subtotalGross - lineDiscountAmount;

        GlobalDiscountPercent = Math.Clamp(GlobalDiscountPercent, 0m, 100m);
        var globalDiscountAmount = subtotalAfterLineDiscount * (GlobalDiscountPercent / 100m);
        var subtotal = subtotalAfterLineDiscount - globalDiscountAmount;
        var tax = subtotal * (taxRate / 100m);
        var total = subtotal + tax;

        var normalizedPayment = string.IsNullOrWhiteSpace(PaymentMethod) ? "Cash" : PaymentMethod;
        if (normalizedPayment.Equals("Cash", StringComparison.OrdinalIgnoreCase) && AmountReceived < total)
        {
            ModelState.AddModelError(string.Empty, "El monto recibido no cubre el total de la venta.");
            return Page();
        }

        var sale = new Sale
        {
            Date = DateTime.UtcNow,
            UserId = userId,
            CustomerId = CustomerId,
            IsInvoice = IsInvoice,
            Status = "Completed",
            PaymentMethod = normalizedPayment,
            AmountReceived = AmountReceived,
            ChangeAmount = normalizedPayment.Equals("Cash", StringComparison.OrdinalIgnoreCase) ? (AmountReceived - total) : 0m,
            SubtotalAmount = subtotal,
            TaxAmount = tax,
            DiscountAmount = lineDiscountAmount + globalDiscountAmount,
            TotalAmount = total
        };

        foreach (var line in selected)
        {
            var lineSubtotal = line.Product.Price * line.Qty;
            var lineDiscount = lineSubtotal * (line.DiscountPercent / 100m);
            sale.Details.Add(new SaleDetail
            {
                ProductId = line.Product.Id,
                Quantity = line.Qty,
                UnitPrice = line.Product.Price,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = lineDiscount
            });

            line.Product.Stock -= line.Qty;
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Venta #{sale.Id} registrada correctamente.";
        return RedirectToPage("/Sales/Ticket", new { id = sale.Id });
    }

    private async Task LoadAsync()
    {
        Products = await db.Products.Include(x => x.Category).OrderBy(x => x.Name).ToListAsync();
        Customers = await db.Customers.Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
    }
}
