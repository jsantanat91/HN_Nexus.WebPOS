using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Inventory;

public class TransfersModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Warehouses { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<StockTransfer> Items { get; private set; } = new();

    [BindProperty]
    public int FromBranchId { get; set; }

    [BindProperty]
    public int ToBranchId { get; set; }

    [BindProperty]
    public int FromWarehouseId { get; set; }

    [BindProperty]
    public int ToWarehouseId { get; set; }

    [BindProperty]
    public int ProductId { get; set; }

    [BindProperty]
    public int Quantity { get; set; }

    [BindProperty]
    public string Notes { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await LoadAsync();

        if (FromBranchId <= 0 || ToBranchId <= 0 || ProductId <= 0 || Quantity <= 0 || FromWarehouseId <= 0 || ToWarehouseId <= 0)
        {
            TempData["Flash"] = "Datos inválidos para transferencia.";
            return RedirectToPage();
        }

        var sourceWarehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == FromWarehouseId && w.BranchId == FromBranchId);
        var targetWarehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == ToWarehouseId && w.BranchId == ToBranchId);
        if (sourceWarehouse is null || targetWarehouse is null)
        {
            TempData["Flash"] = "Almacén origen/destino inválido para las sucursales seleccionadas.";
            return RedirectToPage();
        }

        var sourceWhStock = await db.WarehouseStocks.FirstOrDefaultAsync(ps => ps.WarehouseId == FromWarehouseId && ps.ProductId == ProductId);
        if (sourceWhStock is null || sourceWhStock.Stock < Quantity)
        {
            TempData["Flash"] = "Stock insuficiente en almacén origen.";
            return RedirectToPage();
        }

        var targetWhStock = await db.WarehouseStocks.FirstOrDefaultAsync(ps => ps.WarehouseId == ToWarehouseId && ps.ProductId == ProductId);
        if (targetWhStock is null)
        {
            targetWhStock = new WarehouseStock { ProductId = ProductId, WarehouseId = ToWarehouseId, Stock = 0, MinStock = 5 };
            db.WarehouseStocks.Add(targetWhStock);
        }

        sourceWhStock.Stock -= Quantity;
        targetWhStock.Stock += Quantity;

        var source = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.BranchId == FromBranchId && ps.ProductId == ProductId);
        if (source is not null)
        {
            source.Stock = Math.Max(0, source.Stock - Quantity);
        }

        var target = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.BranchId == ToBranchId && ps.ProductId == ProductId);
        if (target is null)
        {
            target = new ProductStock { ProductId = ProductId, BranchId = ToBranchId, Stock = 0, MinStock = 5 };
            db.ProductStocks.Add(target);
        }
        target.Stock += Quantity;

        var sourceLots = await db.ProductLots
            .Where(x => x.BranchId == FromBranchId && x.ProductId == ProductId && x.Quantity > 0)
            .OrderBy(x => x.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var toMove = Quantity;
        foreach (var lot in sourceLots)
        {
            if (toMove <= 0)
            {
                break;
            }

            var moving = Math.Min(lot.Quantity, toMove);
            lot.Quantity -= moving;
            lot.UpdatedAt = DateTime.UtcNow;
            toMove -= moving;

            var targetLot = await db.ProductLots.FirstOrDefaultAsync(x =>
                x.BranchId == ToBranchId &&
                x.ProductId == ProductId &&
                x.LotNumber == lot.LotNumber &&
                x.SerialNumber == lot.SerialNumber &&
                x.ExpirationDate == lot.ExpirationDate);

            if (targetLot is null)
            {
                db.ProductLots.Add(new ProductLot
                {
                    BranchId = ToBranchId,
                    ProductId = ProductId,
                    LotNumber = lot.LotNumber,
                    SerialNumber = lot.SerialNumber,
                    ExpirationDate = lot.ExpirationDate,
                    Quantity = moving,
                    UnitCost = lot.UnitCost,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                targetLot.Quantity += moving;
                targetLot.UpdatedAt = DateTime.UtcNow;
            }
        }

        db.StockTransfers.Add(new StockTransfer
        {
            ProductId = ProductId,
            FromBranchId = FromBranchId,
            FromWarehouseId = FromWarehouseId,
            ToBranchId = ToBranchId,
            ToWarehouseId = ToWarehouseId,
            Quantity = Quantity,
            Notes = (Notes ?? string.Empty).Trim(),
            UserId = userContext.GetUserId(User),
            CreatedAt = DateTime.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "TRANSFER",
            Entity = "Stock",
            EntityId = ProductId,
            BranchId = FromBranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Transferencia producto {ProductId}: {Quantity} de suc {FromBranchId}/alm {FromWarehouseId} a suc {ToBranchId}/alm {ToWarehouseId}."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Transferencia realizada.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches.OrderBy(b => b.Name).Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();

        var branchIds = branches.Select(b => b.Id).ToList();
        Warehouses = await db.Warehouses
            .Include(w => w.Branch)
            .Where(w => branchIds.Contains(w.BranchId) && w.IsActive)
            .OrderBy(w => w.BranchId)
            .ThenBy(w => w.Name)
            .Select(w => new SelectListItem($"[{w.Branch!.Code}] {w.Name}", w.Id.ToString()))
            .ToListAsync();

        Products = await db.Products
            .Where(p => db.ProductStocks.Any(ps => ps.ProductId == p.Id && branchIds.Contains(ps.BranchId)))
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();

        Items = await db.StockTransfers
            .Include(x => x.Product)
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.FromWarehouse)
            .Include(x => x.ToWarehouse)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
    }
}
