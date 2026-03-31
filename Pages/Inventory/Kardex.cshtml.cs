using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Inventory;

public class KardexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<KardexRow> Rows { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = DateTime.Today;

    public async Task OnGetAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches.OrderBy(b => b.Name).Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0) BranchId = int.Parse(Branches[0].Value!);

        Products = await db.ProductStocks
            .Include(ps => ps.Product)
            .Where(ps => ps.BranchId == BranchId)
            .OrderBy(ps => ps.Product!.Name)
            .Select(ps => new SelectListItem(ps.Product!.Name, ps.ProductId.ToString()))
            .ToListAsync();

        if (ProductId <= 0 && Products.Count > 0) ProductId = int.Parse(Products[0].Value!);

        if (BranchId <= 0 || ProductId <= 0)
        {
            Rows = [];
            return;
        }

        var start = DateTime.SpecifyKind(From.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(To.Date.AddDays(1), DateTimeKind.Utc);

        var saleOut = await db.SaleDetails
            .Include(d => d.Sale)!.ThenInclude(s => s!.Customer)
            .Include(d => d.Sale)!.ThenInclude(s => s!.User)
            .Where(d => d.ProductId == ProductId && d.Sale != null && d.Sale.BranchId == BranchId && d.Sale.Status == "Completed" && d.Sale.Date >= start && d.Sale.Date < end)
            .Select(d => new KardexRow
            {
                Date = d.Sale!.Date,
                Type = "Salida",
                Quantity = d.Quantity,
                Reference = $"Venta #{d.SaleId}",
                Notes = $"{d.Sale.PaymentMethod} | Cliente: {(d.Sale.Customer != null ? d.Sale.Customer.FullName : "Publico General")}",
                Actor = d.Sale.User != null ? d.Sale.User.FullName : "Caja",
                UnitPrice = d.UnitPrice,
                LineTotal = d.Total,
                Source = "Caja"
            })
            .ToListAsync();

        var purchaseIn = await db.SupplierOrders
            .Include(o => o.Supplier)
            .Where(o => o.BranchId == BranchId && o.ProductId == ProductId && o.Status == "Recibido" && o.OrderDate >= start && o.OrderDate < end)
            .Select(o => new KardexRow
            {
                Date = o.OrderDate,
                Type = "Entrada",
                Quantity = o.Quantity,
                Reference = $"OC #{o.Id}",
                Notes = "Recepcion orden de compra",
                Actor = o.Supplier != null ? o.Supplier.Name : "Proveedor",
                UnitPrice = o.UnitCost,
                LineTotal = o.UnitCost * o.Quantity,
                Source = "Orden de compra"
            })
            .ToListAsync();

        var transferOut = await db.StockTransfers
            .Include(t => t.ToBranch)
            .Include(t => t.ToWarehouse)
            .Include(t => t.User)
            .Where(t => t.ProductId == ProductId && t.FromBranchId == BranchId && t.CreatedAt >= start && t.CreatedAt < end)
            .Select(t => new KardexRow
            {
                Date = t.CreatedAt,
                Type = "Salida",
                Quantity = t.Quantity,
                Reference = $"TR #{t.Id}",
                Notes = $"A sucursal {(t.ToBranch != null ? t.ToBranch.Name : t.ToBranchId.ToString())}" + (t.ToWarehouse != null ? $" / Almacen {t.ToWarehouse.Name}" : string.Empty),
                Source = "Transferencia",
                Actor = t.User != null ? t.User.FullName : "Sistema"
            })
            .ToListAsync();

        var transferIn = await db.StockTransfers
            .Include(t => t.FromBranch)
            .Include(t => t.FromWarehouse)
            .Include(t => t.User)
            .Where(t => t.ProductId == ProductId && t.ToBranchId == BranchId && t.CreatedAt >= start && t.CreatedAt < end)
            .Select(t => new KardexRow
            {
                Date = t.CreatedAt,
                Type = "Entrada",
                Quantity = t.Quantity,
                Reference = $"TR #{t.Id}",
                Notes = $"Desde sucursal {(t.FromBranch != null ? t.FromBranch.Name : t.FromBranchId.ToString())}" + (t.FromWarehouse != null ? $" / Almacen {t.FromWarehouse.Name}" : string.Empty),
                Source = "Transferencia",
                Actor = t.User != null ? t.User.FullName : "Sistema"
            })
            .ToListAsync();

        Rows = saleOut
            .Concat(purchaseIn)
            .Concat(transferOut)
            .Concat(transferIn)
            .OrderBy(r => r.Date)
            .ToList();
    }

    public class KardexRow
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? Actor { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineTotal { get; set; }
    }
}
