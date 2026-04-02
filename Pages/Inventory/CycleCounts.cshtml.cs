using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Inventory;

public class CycleCountsModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Warehouses { get; private set; } = new();
    public List<CycleCount> Sessions { get; private set; } = new();
    public List<CycleCountLine> ActiveLines { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int WarehouseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int SessionId { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostStartAsync(int branchId, int warehouseId, string? notes)
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var count = new CycleCount
        {
            BranchId = branchId,
            WarehouseId = warehouseId,
            CreatedByUserId = userId.Value,
            Notes = (notes ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = "Open"
        };

        var stocks = await db.WarehouseStocks.Where(x => x.WarehouseId == warehouseId).ToListAsync();
        foreach (var s in stocks)
        {
            count.Lines.Add(new CycleCountLine
            {
                ProductId = s.ProductId,
                SystemQty = s.Stock,
                CountedQty = s.Stock
            });
        }

        db.CycleCounts.Add(count);
        await db.SaveChangesAsync();
        TempData["Flash"] = $"Conteo #{count.Id} iniciado.";
        return RedirectToPage(new { branchId, warehouseId, sessionId = count.Id });
    }

    public async Task<IActionResult> OnPostUpdateLineAsync(int id, int countedQty, int branchId, int warehouseId, int sessionId)
    {
        var line = await db.CycleCountLines.Include(x => x.CycleCount).FirstOrDefaultAsync(x => x.Id == id);
        if (line is null || line.CycleCount is null || line.CycleCount.Status != "Open")
        {
            return RedirectToPage(new { branchId, warehouseId, sessionId });
        }

        line.CountedQty = Math.Max(0, countedQty);
        await db.SaveChangesAsync();
        return RedirectToPage(new { branchId, warehouseId, sessionId });
    }

    public async Task<IActionResult> OnPostAuthorizeAsync(int sessionId, int branchId, int warehouseId)
    {
        if (!User.IsInRole("Admin"))
        {
            TempData["Flash"] = "Solo administrador puede autorizar diferencias.";
            return RedirectToPage(new { branchId, warehouseId, sessionId });
        }

        var userId = userContext.GetUserId(User);
        var session = await db.CycleCounts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == sessionId);
        if (session is null || session.Status != "Open")
        {
            return RedirectToPage(new { branchId, warehouseId });
        }

        var productIds = session.Lines.Select(x => x.ProductId).Distinct().ToList();
        var whStocks = await db.WarehouseStocks.Where(x => x.WarehouseId == session.WarehouseId && productIds.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        var branchStocks = await db.ProductStocks.Where(x => x.BranchId == session.BranchId && productIds.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);

        foreach (var line in session.Lines)
        {
            if (whStocks.TryGetValue(line.ProductId, out var w))
            {
                w.Stock = line.CountedQty;
            }

            if (branchStocks.TryGetValue(line.ProductId, out var b))
            {
                b.Stock = line.CountedQty;
            }

            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == line.ProductId);
            if (p is not null)
            {
                p.Stock = line.CountedQty;
            }

            db.LotTraces.Add(new LotTrace
            {
                BranchId = session.BranchId,
                ProductId = line.ProductId,
                ProductLotId = null,
                Quantity = line.Difference,
                MovementType = "Adjust",
                Reference = $"Conteo cíclico #{session.Id}",
                CreatedAt = DateTime.UtcNow
            });
        }

        session.Status = "Authorized";
        session.AuthorizedByUserId = userId;
        session.AuthorizedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        TempData["Flash"] = $"Conteo #{session.Id} autorizado y aplicado.";
        return RedirectToPage(new { branchId, warehouseId, sessionId });
    }

    private async Task LoadAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches.OrderBy(x => x.Name).Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Warehouses = await db.Warehouses.Where(x => x.BranchId == BranchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync();
        if (WarehouseId <= 0 && Warehouses.Count > 0)
        {
            WarehouseId = int.Parse(Warehouses[0].Value!);
        }

        Sessions = await db.CycleCounts
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BranchId == BranchId && x.WarehouseId == WarehouseId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync();

        if (SessionId <= 0 && Sessions.Count > 0)
        {
            SessionId = Sessions[0].Id;
        }

        ActiveLines = await db.CycleCountLines
            .Include(x => x.Product)
            .Where(x => x.CycleCountId == SessionId)
            .OrderBy(x => x.Product!.Name)
            .ToListAsync();
    }
}
