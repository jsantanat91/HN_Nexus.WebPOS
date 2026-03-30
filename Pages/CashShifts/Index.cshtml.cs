using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.CashShifts;

public class IndexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<CashShift> Shifts { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty]
    public decimal OpeningFloat { get; set; }

    [BindProperty]
    public decimal ClosingDeclared { get; set; }

    public CashShift? OpenShift { get; private set; }

    public async Task OnGetAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var userId = userContext.GetUserId(User);
        if (userId is not null)
        {
            OpenShift = await db.CashShifts
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.BranchId == BranchId && x.Status == "Open");
        }

        Shifts = await db.CashShifts
            .Include(x => x.Branch)
            .Include(x => x.User)
            .Where(x => BranchId <= 0 || x.BranchId == BranchId)
            .OrderByDescending(x => x.OpenedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostOpenAsync(int branchId, decimal openingFloat)
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var exists = await db.CashShifts.AnyAsync(x => x.UserId == userId && x.BranchId == branchId && x.Status == "Open");
        if (exists)
        {
            TempData["Flash"] = "Ya tienes un turno abierto en esta sucursal.";
            return RedirectToPage(new { branchId });
        }

        db.CashShifts.Add(new CashShift
        {
            BranchId = branchId,
            UserId = userId.Value,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = openingFloat,
            Status = "Open"
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Turno abierto correctamente.";
        return RedirectToPage(new { branchId });
    }

    public async Task<IActionResult> OnPostCloseAsync(int shiftId, int branchId, decimal closingDeclared)
    {
        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Id == shiftId);
        if (shift is null || shift.Status != "Open")
        {
            return RedirectToPage(new { branchId });
        }

        var cashTotal = await db.Sales
            .Where(x => x.BranchId == shift.BranchId && x.Status == "Completed" && x.PaymentMethod == "Cash" && x.Date >= shift.OpenedAt)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        shift.ClosedAt = DateTime.UtcNow;
        shift.ClosingDeclared = closingDeclared;
        shift.SystemCashTotal = shift.OpeningFloat + cashTotal;
        shift.Difference = closingDeclared - shift.SystemCashTotal;
        shift.Status = "Closed";

        await db.SaveChangesAsync();
        TempData["Flash"] = "Turno cerrado.";
        return RedirectToPage(new { branchId });
    }
}
