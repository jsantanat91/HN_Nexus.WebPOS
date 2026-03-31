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
    public List<SelectListItem> Warehouses { get; private set; } = new();
    public AppConfig Config { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int WarehouseId { get; set; }

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
            return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
        }

        if (string.IsNullOrWhiteSpace(QuickProductName) || QuickProductPrice <= 0)
        {
            TempData["Flash"] = "Completa nombre y precio del producto rápido.";
            return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
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

        var warehouseId = WarehouseId > 0
            ? WarehouseId
            : await db.Warehouses.Where(w => w.BranchId == BranchId && w.IsActive).OrderBy(w => w.Id).Select(w => w.Id).FirstOrDefaultAsync();
        if (warehouseId > 0)
        {
            db.WarehouseStocks.Add(new WarehouseStock
            {
                ProductId = product.Id,
                WarehouseId = warehouseId,
                Stock = QuickProductStock,
                MinStock = 5
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "QUICK_CREATE",
            Entity = "Product",
            EntityId = product.Id,
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HN_Nexus.WebPOS.Services.ClientIpResolver.Get(HttpContext),
            Details = $"Producto rápido '{product.Name}' desde Caja."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Producto rápido agregado.";
        return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
    }

    public async Task<IActionResult> OnPostQuickCustomerAsync()
    {
        await LoadAsync();

        if (string.IsNullOrWhiteSpace(QuickCustomerName))
        {
            TempData["Flash"] = "Nombre de cliente requerido.";
            return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
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
                return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
            }

            cp = (QuickCustomerPostalCode ?? string.Empty).Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(cp, @"^\d{5}$"))
            {
                TempData["Flash"] = "Código postal inválido (debe ser de 5 dígitos) para factura.";
                return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
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
            IpAddress = HN_Nexus.WebPOS.Services.ClientIpResolver.Get(HttpContext),
            Details = $"Cliente rápido '{QuickCustomerName.Trim()}' desde Caja."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente rápido agregado.";
        return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        var closureDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var hasClosedPeriod = await db.AccountingClosures.AnyAsync(x =>
            x.BranchId == BranchId &&
            x.ClosureDate == closureDate &&
            x.Status == "Closed");
        if (hasClosedPeriod)
        {
            TempData["Flash"] = "La sucursal tiene cierre contable activo para hoy. Requiere reapertura auditada.";
            return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
        }

        if (WarehouseId <= 0)
        {
            WarehouseId = await db.Warehouses
                .Where(w => w.BranchId == BranchId && w.IsActive)
                .OrderBy(w => w.Id)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();
        }

        if (WarehouseId <= 0)
        {
            TempData["Flash"] = "No hay almacenes activos en la sucursal.";
            return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId });
        }

        var stockRows = await db.WarehouseStocks
            .Include(ps => ps.Product)
            .Where(ps => ps.WarehouseId == WarehouseId)
            .ToListAsync();

        var selected = new List<(WarehouseStock Stock, int Qty, decimal DiscountPercent)>();
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
        var nowLocal = DateTime.Now;
        var activePromotions = await db.PromotionRules
            .Where(x => x.IsActive && (x.BranchId == null || x.BranchId == BranchId))
            .ToListAsync();

        bool IsRuleActiveNow(PromotionRule rule)
        {
            if (rule.StartTime is null || rule.EndTime is null)
            {
                return true;
            }

            var nowTime = TimeOnly.FromDateTime(nowLocal);
            var inRange = rule.StartTime <= rule.EndTime
                ? nowTime >= rule.StartTime && nowTime <= rule.EndTime
                : nowTime >= rule.StartTime || nowTime <= rule.EndTime;
            if (!inRange)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(rule.DaysOfWeekCsv))
            {
                return true;
            }

            var today = ((int)nowLocal.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            return rule.DaysOfWeekCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => x == today);
        }

        decimal PromoDiscount(WarehouseStock stock, int qty)
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

            decimal advanced = 0m;
            foreach (var rule in activePromotions.Where(r => r.ProductId == product.Id && IsRuleActiveNow(r)))
            {
                if (rule.RuleType == "ThreeForTwo" && rule.BuyQty > 0 && rule.PayQty >= 0 && rule.BuyQty > rule.PayQty)
                {
                    var freeByBlock = rule.BuyQty - rule.PayQty;
                    var freeItems = (qty / rule.BuyQty) * freeByBlock;
                    advanced += freeItems * unit;
                    continue;
                }

                if (rule.RuleType == "HappyHourPercent" && rule.DiscountPercent > 0)
                {
                    advanced += (unit * qty) * (rule.DiscountPercent / 100m);
                }
            }

            return advanced;
        }

        var selectedByProduct = selected.ToDictionary(x => x.Stock.ProductId, x => x.Qty);
        decimal comboDiscountAmount = 0m;
        foreach (var rule in activePromotions.Where(r => r.RuleType == "ComboPrice" && IsRuleActiveNow(r)))
        {
            if (string.IsNullOrWhiteSpace(rule.ProductIdsCsv) || rule.ComboPrice <= 0)
            {
                continue;
            }

            var comboIds = rule.ProductIdsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (comboIds.Count < 2 || comboIds.Any(id => !selectedByProduct.ContainsKey(id)))
            {
                continue;
            }

            var combosCount = comboIds.Min(id => selectedByProduct[id]);
            if (combosCount <= 0)
            {
                continue;
            }

            var regularValue = comboIds.Sum(id =>
            {
                var line = selected.FirstOrDefault(x => x.Stock.ProductId == id);
                return line.Stock.Product?.Price ?? 0m;
            }) * combosCount;

            var promoValue = rule.ComboPrice * combosCount;
            if (regularValue > promoValue)
            {
                comboDiscountAmount += (regularValue - promoValue);
            }
        }

        var customer = CustomerId.HasValue
            ? await db.Customers.Include(c => c.PriceList).FirstOrDefaultAsync(c => c.Id == CustomerId.Value)
            : null;

        var priceListId = customer?.PriceListId;
        var ruleCandidates = priceListId.HasValue
            ? await db.PriceListItems.Where(x => x.PriceListId == priceListId.Value).ToListAsync()
            : [];

        decimal ResolveUnitPrice(int productId, int qty, decimal basePrice)
        {
            if (!priceListId.HasValue)
            {
                return basePrice;
            }

            var rule = ruleCandidates
                .Where(x => x.ProductId == productId && x.MinQty <= qty)
                .OrderByDescending(x => x.MinQty)
                .FirstOrDefault();
            return rule?.Price ?? basePrice;
        }

        var subtotalGross = selected.Sum(x => ResolveUnitPrice(x.Stock.ProductId, x.Qty, x.Stock.Product?.Price ?? 0m) * x.Qty);
        var promoDiscountAmount = selected.Sum(x => PromoDiscount(x.Stock, x.Qty)) + comboDiscountAmount;
        var lineDiscountAmount = selected.Sum(x => ((ResolveUnitPrice(x.Stock.ProductId, x.Qty, x.Stock.Product?.Price ?? 0m) * x.Qty) - PromoDiscount(x.Stock, x.Qty)) * (x.DiscountPercent / 100m));
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
            WarehouseId = WarehouseId,
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
            TotalAmount = total,
            CfdiStatus = IsInvoice ? "Pendiente" : "NoAplica"
        };

        foreach (var line in selected)
        {
            var unitPrice = ResolveUnitPrice(line.Stock.ProductId, line.Qty, line.Stock.Product?.Price ?? 0m);
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

            var branchStock = await db.ProductStocks.FirstOrDefaultAsync(x => x.BranchId == BranchId && x.ProductId == line.Stock.ProductId);
            if (branchStock is not null)
            {
                branchStock.Stock = Math.Max(0, branchStock.Stock - line.Qty);
            }

            var lotOk = await ConsumeLotsAsync(BranchId, line.Stock.ProductId, line.Qty);
            if (!lotOk)
            {
                ModelState.AddModelError(string.Empty, $"Lotes insuficientes para el producto {line.Stock.Product?.Name}. Ajusta lotes/caducidades.");
                return Page();
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
            IpAddress = HN_Nexus.WebPOS.Services.ClientIpResolver.Get(HttpContext),
            Details = $"Venta registrada. Total={total:N2}, método={normalizedPayment}, items={selected.Count}."
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Venta #{sale.Id} registrada correctamente.";
        return RedirectToPage(new { branchId = BranchId, warehouseId = WarehouseId, ticketSaleId = sale.Id });
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

        Warehouses = await db.Warehouses
            .Where(w => w.BranchId == BranchId && w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new SelectListItem($"{w.Code} - {w.Name}", w.Id.ToString()))
            .ToListAsync();

        if (WarehouseId <= 0 && Warehouses.Count > 0)
        {
            WarehouseId = int.Parse(Warehouses[0].Value!);
        }

        var branchStocks = await db.WarehouseStocks
            .Include(ps => ps.Product)!.ThenInclude(p => p!.Category)
            .Where(ps => ps.WarehouseId == WarehouseId)
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

        Customers = await db.Customers
            .Include(x => x.PriceList)
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(
                x.PriceListId == null ? x.FullName : $"{x.FullName} ({x.PriceList!.Name})",
                x.Id.ToString()))
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

    private async Task<bool> ConsumeLotsAsync(int branchId, int productId, int quantity)
    {
        var lots = await db.ProductLots
            .Where(x => x.BranchId == branchId && x.ProductId == productId && x.Quantity > 0)
            .OrderBy(x => x.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (lots.Count == 0)
        {
            return true;
        }

        var available = lots.Sum(x => x.Quantity);
        if (available < quantity)
        {
            return false;
        }

        var pending = quantity;
        foreach (var lot in lots)
        {
            if (pending <= 0)
            {
                break;
            }

            var take = Math.Min(lot.Quantity, pending);
            lot.Quantity -= take;
            lot.UpdatedAt = DateTime.UtcNow;
            pending -= take;
        }

        return pending <= 0;
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




