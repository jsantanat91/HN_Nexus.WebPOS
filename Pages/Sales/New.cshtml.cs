using System.Globalization;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class NewModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<ProductPosItem> Products { get; private set; } = new();
    public List<SelectListItem> Customers { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();
    public AppConfig Config { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? TicketSaleId { get; set; }

    [BindProperty]
    public int? CustomerId { get; set; }

    [BindProperty]
    public bool IsInvoice { get; set; }

    [BindProperty]
    public bool PricesIncludeTax { get; set; }

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string AuthorizationCode { get; set; } = string.Empty;

    [BindProperty]
    public decimal AmountReceived { get; set; }

    [BindProperty]
    public decimal GlobalDiscountPercent { get; set; }

    [BindProperty]
    public string QuickProductName { get; set; } = string.Empty;

    [BindProperty]
    public string QuickProductCode { get; set; } = string.Empty;

    [BindProperty]
    public decimal QuickProductPrice { get; set; }

    [BindProperty]
    public int QuickProductStock { get; set; }

    [BindProperty]
    public string QuickCustomerName { get; set; } = string.Empty;

    [BindProperty]
    public string QuickCustomerRfc { get; set; } = string.Empty;

    [BindProperty]
    public string QuickCustomerPostalCode { get; set; } = string.Empty;

    [BindProperty]
    public string QuickCustomerEmail { get; set; } = string.Empty;

    [BindProperty]
    public string QuickCustomerCfdiUse { get; set; } = "G03";

    [BindProperty]
    public string QuickCustomerPaymentForm { get; set; } = "01";

    [BindProperty]
    public string QuickCustomerPaymentMethodSat { get; set; } = "PUE";

    [BindProperty]
    public bool QuickCustomerRequiresInvoice { get; set; }

    public List<SelectListItem> QuickCfdiUses { get; private set; } = new();
    public List<SelectListItem> QuickPaymentForms { get; private set; } = new();
    public List<SelectListItem> QuickPaymentMethods { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Branches.Count == 0)
        {
            TempData["Flash"] = "No tienes sucursales asignadas. Solicita acceso al administrador.";
            return RedirectToPage("/Account/Denied");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostQuickProductAsync()
    {
        await LoadAsync();
        if (BranchId <= 0)
        {
            TempData["Flash"] = "Selecciona sucursal para crear producto rápido.";
            return RedirectToPage(new { branchId = BranchId });
        }

        if (string.IsNullOrWhiteSpace(QuickProductName) || QuickProductPrice <= 0)
        {
            TempData["Flash"] = "Completa nombre y precio del producto rápido.";
            return RedirectToPage(new { branchId = BranchId });
        }

        var cat = await db.Categories.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (cat is null)
        {
            cat = new Category { Name = "General" };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
        }

        var code = string.IsNullOrWhiteSpace(QuickProductCode)
            ? $"QK-{DateTime.UtcNow:HHmmss}"
            : QuickProductCode.Trim();

        var product = new Product
        {
            ProductNumber = (await db.Products.MaxAsync(x => (int?)x.ProductNumber) ?? 0) + 1,
            Name = QuickProductName.Trim(),
            Barcode = code,
            Price = QuickProductPrice,
            Cost = QuickProductPrice * 0.65m,
            Stock = QuickProductStock,
            PriceIncludesTax = false,
            CategoryId = cat.Id,
            SatProductCode = "01010101",
            SatUnitCode = "H87"
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.ProductStocks.Add(new ProductStock
        {
            ProductId = product.Id,
            BranchId = BranchId,
            Stock = QuickProductStock,
            MinStock = 5
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "QUICK_CREATE",
            Entity = "Product",
            EntityId = product.Id,
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Producto rápido '{product.Name}' desde Caja."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Producto rápido agregado.";
        return RedirectToPage(new { branchId = BranchId });
    }

    public async Task<IActionResult> OnPostQuickCustomerAsync()
    {
        await LoadAsync();

        if (string.IsNullOrWhiteSpace(QuickCustomerName))
        {
            TempData["Flash"] = "Nombre de cliente requerido.";
            return RedirectToPage(new { branchId = BranchId });
        }

        var rfc = "XAXX010101000";
        var cp = string.Empty;
        var cfdiUse = "S01";
        var paymentForm = "01";
        var paymentMethodSat = "PUE";

        if (QuickCustomerRequiresInvoice)
        {
            rfc = (string.IsNullOrWhiteSpace(QuickCustomerRfc) ? string.Empty : QuickCustomerRfc.Trim().ToUpperInvariant());
            if (!System.Text.RegularExpressions.Regex.IsMatch(rfc, @"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$"))
            {
                TempData["Flash"] = "RFC inválido para cliente rápido con factura.";
                return RedirectToPage(new { branchId = BranchId });
            }

            cp = (QuickCustomerPostalCode ?? string.Empty).Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(cp, @"^\d{5}$"))
            {
                TempData["Flash"] = "Código postal inválido (debe ser de 5 dígitos) para factura.";
                return RedirectToPage(new { branchId = BranchId });
            }

            cfdiUse = QuickCustomerCfdiUse;
            paymentForm = QuickCustomerPaymentForm;
            paymentMethodSat = QuickCustomerPaymentMethodSat;
        }

        db.Customers.Add(new Customer
        {
            FullName = QuickCustomerName.Trim(),
            Rfc = rfc,
            Email = (QuickCustomerEmail ?? string.Empty).Trim(),
            PhoneNumber = string.Empty,
            Address = string.Empty,
            PostalCode = cp,
            CfdiUse = cfdiUse,
            FiscalRegime = "601",
            InvoiceType = "I",
            PaymentForm = paymentForm,
            PaymentMethodSat = paymentMethodSat,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "QUICK_CREATE",
            Entity = "Customer",
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Cliente rápido '{QuickCustomerName.Trim()}' desde Caja."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente rápido agregado.";
        return RedirectToPage(new { branchId = BranchId });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        var stockRows = await db.ProductStocks
            .Include(ps => ps.Product)
            .Where(ps => ps.BranchId == BranchId)
            .ToListAsync();

        var selected = new List<(ProductStock Stock, int Qty, decimal DiscountPercent)>();
        foreach (var row in stockRows)
        {
            var key = $"qty_{row.ProductId}";
            if (!Request.Form.TryGetValue(key, out var qtyRaw))
            {
                continue;
            }

            if (!int.TryParse(qtyRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                continue;
            }

            var discountRaw = Request.Form[$"disc_{row.ProductId}"].FirstOrDefault() ?? "0";
            _ = decimal.TryParse(discountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var lineDiscountPercent);
            lineDiscountPercent = Math.Clamp(lineDiscountPercent, 0m, 100m);

            if (row.Stock < qty)
            {
                ModelState.AddModelError(string.Empty, $"Stock insuficiente para {row.Product?.Name}. Disponible: {row.Stock}.");
                return Page();
            }

            selected.Add((row, qty, lineDiscountPercent));
        }

        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debes capturar al menos un producto con cantidad > 0.");
            return Page();
        }

        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var taxRate = Math.Clamp(Config.TaxRate, 0m, 100m) / 100m;
        decimal PromoDiscount(ProductStock stock, int qty)
        {
            var product = stock.Product;
            if (product is null || qty <= 0)
            {
                return 0m;
            }

            var unit = product.Price;
            if (product.PromotionType == "TwoForOne")
            {
                var free = qty / 2;
                return free * unit;
            }

            if (product.PromotionType == "Volume" && product.PromotionMinQty > 0 && qty >= product.PromotionMinQty && product.PromotionValue > 0)
            {
                return (unit * qty) * (product.PromotionValue / 100m);
            }

            return 0m;
        }

        var subtotalGross = selected.Sum(x => (x.Stock.Product?.Price ?? 0m) * x.Qty);
        var promoDiscountAmount = selected.Sum(x => PromoDiscount(x.Stock, x.Qty));
        var lineDiscountAmount = selected.Sum(x => (((x.Stock.Product?.Price ?? 0m) * x.Qty) - PromoDiscount(x.Stock, x.Qty)) * (x.DiscountPercent / 100m));
        var subtotalAfterLineDiscount = subtotalGross - lineDiscountAmount;
        subtotalAfterLineDiscount -= promoDiscountAmount;

        GlobalDiscountPercent = Math.Clamp(GlobalDiscountPercent, 0m, 100m);
        var globalDiscountAmount = subtotalAfterLineDiscount * (GlobalDiscountPercent / 100m);
        var discounted = subtotalAfterLineDiscount - globalDiscountAmount;

        decimal subtotal;
        decimal tax;
        decimal total;

        if (PricesIncludeTax)
        {
            total = discounted;
            subtotal = taxRate <= 0 ? discounted : discounted / (1 + taxRate);
            tax = total - subtotal;
        }
        else
        {
            subtotal = discounted;
            tax = subtotal * taxRate;
            total = subtotal + tax;
        }

        var normalizedPayment = string.IsNullOrWhiteSpace(PaymentMethod) ? "Cash" : PaymentMethod;

        if (normalizedPayment.Equals("Cash", StringComparison.OrdinalIgnoreCase) && AmountReceived < total)
        {
            ModelState.AddModelError(string.Empty, "El monto recibido no cubre el total de la venta.");
            return Page();
        }

        if (normalizedPayment.Equals("Card", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(AuthorizationCode))
        {
            ModelState.AddModelError(string.Empty, "Captura el número de autorización del ticket de tarjeta.");
            return Page();
        }

        var sale = new Sale
        {
            Date = DateTime.UtcNow,
            BranchId = BranchId,
            UserId = userId.Value,
            CustomerId = CustomerId,
            IsInvoice = IsInvoice,
            Status = "Completed",
            PaymentMethod = normalizedPayment,
            AuthorizationCode = string.IsNullOrWhiteSpace(AuthorizationCode) ? null : AuthorizationCode.Trim(),
            PricesIncludeTax = PricesIncludeTax,
            AmountReceived = AmountReceived,
            ChangeAmount = normalizedPayment.Equals("Cash", StringComparison.OrdinalIgnoreCase) ? (AmountReceived - total) : 0m,
            SubtotalAmount = subtotal,
            TaxAmount = tax,
            DiscountAmount = promoDiscountAmount + lineDiscountAmount + globalDiscountAmount,
            TotalAmount = total
        };

        foreach (var line in selected)
        {
            var unitPrice = line.Stock.Product?.Price ?? 0m;
            var lineSubtotal = unitPrice * line.Qty;
            var promo = PromoDiscount(line.Stock, line.Qty);
            var lineDiscount = (lineSubtotal - promo) * (line.DiscountPercent / 100m);
            sale.Details.Add(new SaleDetail
            {
                ProductId = line.Stock.ProductId,
                Quantity = line.Qty,
                UnitPrice = unitPrice,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = promo + lineDiscount
            });

            line.Stock.Stock -= line.Qty;
            if (line.Stock.Product is not null)
            {
                line.Stock.Product.Stock = Math.Max(0, line.Stock.Product.Stock - line.Qty);
            }
        }

        db.Sales.Add(sale);
        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CREATE",
            Entity = "Sale",
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Venta registrada. Total={total:N2}, método={normalizedPayment}, items={selected.Count}."
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Venta #{sale.Id} registrada correctamente.";
        return RedirectToPage(new { branchId = BranchId, ticketSaleId = sale.Id });
    }

    private async Task LoadAsync()
    {
        var accessibleBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = accessibleBranches
            .Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString()))
            .ToList();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var branchStocks = await db.ProductStocks
            .Include(ps => ps.Product)!.ThenInclude(p => p!.Category)
            .Where(ps => ps.BranchId == BranchId)
            .OrderBy(ps => ps.Product!.Name)
            .ToListAsync();

        Products = branchStocks.Select(ps => new ProductPosItem
        {
            ProductId = ps.ProductId,
            Name = ps.Product?.Name ?? string.Empty,
            Barcode = ps.Product?.Barcode ?? string.Empty,
            Price = ps.Product?.Price ?? 0m,
            PriceIncludesTax = ps.Product?.PriceIncludesTax ?? false,
            CategoryName = ps.Product?.Category?.Name ?? "General",
            PromotionType = ps.Product?.PromotionType ?? "None",
            PromotionValue = ps.Product?.PromotionValue ?? 0m,
            PromotionMinQty = ps.Product?.PromotionMinQty ?? 0,
            Stock = ps.Stock
        }).ToList();

        Customers = await db.Customers.Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();

        QuickCfdiUses = SatCatalogs.UsoCfdi
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code))
            .ToList();
        QuickPaymentForms = SatCatalogs.FormaPago
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code))
            .ToList();
        QuickPaymentMethods = SatCatalogs.MetodoPago
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code))
            .ToList();

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
    }

    public class ProductPosItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool PriceIncludesTax { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string PromotionType { get; set; } = "None";
        public decimal PromotionValue { get; set; }
        public int PromotionMinQty { get; set; }
        public int Stock { get; set; }
    }
}


