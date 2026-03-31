using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.CashCuts;

[Authorize(Policy = "AdminOnly")]
public class AccountingCloseModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<AccountingClosure> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCloseAsync(int branchId, DateTime date, string? notes)
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var closureDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var exists = await db.AccountingClosures.AnyAsync(x =>
            x.BranchId == branchId &&
            x.ClosureDate == closureDate &&
            x.Status == "Closed");
        if (exists)
        {
            TempData["Flash"] = "Ya existe cierre contable para esa fecha/sucursal.";
            return RedirectToPage(new { branchId, date = date.ToString("yyyy-MM-dd") });
        }

        db.AccountingClosures.Add(new AccountingClosure
        {
            BranchId = branchId,
            ClosureDate = closureDate,
            Status = "Closed",
            ClosedAt = DateTime.UtcNow,
            ClosedByUserId = userId.Value,
            Notes = (notes ?? string.Empty).Trim()
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "ACCOUNT_CLOSE",
            Entity = "AccountingClosure",
            BranchId = branchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HN_Nexus.WebPOS.Services.ClientIpResolver.Get(HttpContext),
            Details = $"Cierre contable {closureDate:yyyy-MM-dd}. {notes}"
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Cierre contable aplicado.";
        return RedirectToPage(new { branchId, date = date.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostReopenAsync(int id, int branchId, DateTime date, string reason)
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var item = await db.AccountingClosures.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return RedirectToPage(new { branchId, date = date.ToString("yyyy-MM-dd") });
        }

        item.Status = "Reopened";
        item.ReopenedAt = DateTime.UtcNow;
        item.ReopenedByUserId = userId.Value;
        item.ReopenReason = string.IsNullOrWhiteSpace(reason) ? "Reapertura manual" : reason.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "ACCOUNT_REOPEN",
            Entity = "AccountingClosure",
            EntityId = item.Id,
            BranchId = item.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HN_Nexus.WebPOS.Services.ClientIpResolver.Get(HttpContext),
            Details = $"Reapertura auditada. Motivo: {item.ReopenReason}"
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Cierre reabierto con auditoría.";
        return RedirectToPage(new { branchId, date = date.ToString("yyyy-MM-dd") });
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

        Items = await db.AccountingClosures
            .Include(x => x.Branch)
            .Include(x => x.ClosedByUser)
            .Include(x => x.ReopenedByUser)
            .Where(x => BranchId <= 0 || x.BranchId == BranchId)
            .OrderByDescending(x => x.ClosureDate)
            .Take(180)
            .ToListAsync();
    }
}



