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
    public List<SelectListItem> Products { get; private set; } = new();
    public List<StockTransfer> Items { get; private set; } = new();

    [BindProperty]
    public int FromBranchId { get; set; }

    [BindProperty]
    public int ToBranchId { get; set; }

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

        if (FromBranchId <= 0 || ToBranchId <= 0 || ProductId <= 0 || Quantity <= 0 || FromBranchId == ToBranchId)
        {
            TempData["Flash"] = "Datos inválidos para transferencia.";
            return RedirectToPage();
        }

        var source = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.BranchId == FromBranchId && ps.ProductId == ProductId);
        if (source is null || source.Stock < Quantity)
        {
            TempData["Flash"] = "Stock insuficiente en sucursal origen.";
            return RedirectToPage();
        }

        var target = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.BranchId == ToBranchId && ps.ProductId == ProductId);
        if (target is null)
        {
            target = new ProductStock { ProductId = ProductId, BranchId = ToBranchId, Stock = 0, MinStock = 5 };
            db.ProductStocks.Add(target);
        }

        source.Stock -= Quantity;
        target.Stock += Quantity;

        db.StockTransfers.Add(new StockTransfer
        {
            ProductId = ProductId,
            FromBranchId = FromBranchId,
            ToBranchId = ToBranchId,
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
            Details = $"Transferencia producto {ProductId}: {Quantity} de sucursal {FromBranchId} a {ToBranchId}."
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

        Products = await db.ProductStocks
            .Include(ps => ps.Product)
            .Where(ps => branchIds.Contains(ps.BranchId))
            .GroupBy(ps => new { ps.ProductId, ps.Product!.Name })
            .Select(g => new SelectListItem($"{g.Key.Name}", g.Key.ProductId.ToString()))
            .OrderBy(x => x.Text)
            .ToListAsync();

        Items = await db.StockTransfers
            .Include(x => x.Product)
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
    }
}
