using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Inventory;

public class LotsModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<ProductLot> Items { get; private set; } = new();
    public List<LotTrace> Traces { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ProductId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(int branchId, int productId, string lotNumber, string? serialNumber, DateTime? expirationDate, int quantity, decimal unitCost)
    {
        BranchId = branchId;
        ProductId = productId;

        if (branchId <= 0 || productId <= 0 || quantity <= 0 || string.IsNullOrWhiteSpace(lotNumber))
        {
            TempData["Flash"] = "Captura sucursal, producto, lote y cantidad válidos.";
            return RedirectToPage(new { branchId, productId });
        }

        var lot = await db.ProductLots.FirstOrDefaultAsync(x =>
            x.BranchId == branchId &&
            x.ProductId == productId &&
            x.LotNumber == lotNumber.Trim() &&
            x.SerialNumber == (string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim()));

        if (lot is null)
        {
            db.ProductLots.Add(new ProductLot
            {
                BranchId = branchId,
                ProductId = productId,
                LotNumber = lotNumber.Trim().ToUpperInvariant(),
                SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim().ToUpperInvariant(),
                ExpirationDate = expirationDate.HasValue ? DateTime.SpecifyKind(expirationDate.Value.Date, DateTimeKind.Utc) : null,
                Quantity = quantity,
                UnitCost = unitCost < 0 ? 0 : unitCost,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            lot.Quantity += quantity;
            lot.UnitCost = unitCost > 0 ? unitCost : lot.UnitCost;
            if (expirationDate.HasValue)
            {
                lot.ExpirationDate = DateTime.SpecifyKind(expirationDate.Value.Date, DateTimeKind.Utc);
            }
            lot.UpdatedAt = DateTime.UtcNow;
        }

        var stock = await db.ProductStocks.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId);
        if (stock is null)
        {
            db.ProductStocks.Add(new ProductStock
            {
                BranchId = branchId,
                ProductId = productId,
                Stock = quantity,
                MinStock = 5
            });
        }
        else
        {
            stock.Stock += quantity;
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (product is not null)
        {
            product.Stock += quantity;
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Lote guardado.";
        return RedirectToPage(new { branchId, productId });
    }

    public async Task<IActionResult> OnPostAdjustAsync(int id, int branchId, int productId, int quantity)
    {
        BranchId = branchId;
        ProductId = productId;
        var lot = await db.ProductLots.FirstOrDefaultAsync(x => x.Id == id);
        if (lot is null)
        {
            return RedirectToPage(new { branchId, productId });
        }

        var oldQty = lot.Quantity;
        lot.Quantity = Math.Max(0, quantity);
        lot.UpdatedAt = DateTime.UtcNow;

        var delta = lot.Quantity - oldQty;
        var stock = await db.ProductStocks.FirstOrDefaultAsync(x => x.BranchId == lot.BranchId && x.ProductId == lot.ProductId);
        if (stock is not null)
        {
            stock.Stock = Math.Max(0, stock.Stock + delta);
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == lot.ProductId);
        if (product is not null)
        {
            product.Stock = Math.Max(0, product.Stock + delta);
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Cantidad de lote ajustada.";
        return RedirectToPage(new { branchId, productId });
    }

    private async Task LoadAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToList();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Products = await db.ProductStocks
            .Include(ps => ps.Product)
            .Where(ps => ps.BranchId == BranchId)
            .OrderBy(ps => ps.Product!.Name)
            .Select(ps => new SelectListItem(ps.Product!.Name, ps.ProductId.ToString()))
            .ToListAsync();

        if (ProductId <= 0 && Products.Count > 0)
        {
            ProductId = int.Parse(Products[0].Value!);
        }

        Items = await db.ProductLots
            .Include(x => x.Product)
            .Include(x => x.Branch)
            .Where(x => (BranchId <= 0 || x.BranchId == BranchId) && (ProductId <= 0 || x.ProductId == ProductId))
            .OrderBy(x => x.ExpirationDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(300)
            .ToListAsync();

        Traces = await db.LotTraces
            .Include(x => x.Product)
            .Include(x => x.Sale)
            .Where(x => x.BranchId == BranchId && (ProductId <= 0 || x.ProductId == ProductId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(120)
            .ToListAsync();
    }
}
