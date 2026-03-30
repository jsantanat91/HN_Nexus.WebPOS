using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.CashCuts;

public class IndexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<CashCut> Items { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public decimal SuggestedSystemTotal { get; private set; }
    public decimal SuggestedCashTotal { get; private set; }
    public decimal SuggestedCardTotal { get; private set; }
    public decimal SuggestedTransferTotal { get; private set; }

    public async Task OnGetAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Items = await db.CashCuts
            .Include(x => x.Branch)
            .Where(x => BranchId <= 0 || x.BranchId == BranchId)
            .OrderByDescending(x => x.CutDate)
            .Take(100)
            .ToListAsync();

        await LoadTodayTotalsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(decimal totalPhysical)
    {
        await LoadTodayTotalsAsync();

        var name = User.Identity?.Name ?? User.FindFirstValue("username") ?? "Usuario";
        var cut = new CashCut
        {
            CutDate = DateTime.UtcNow,
            BranchId = BranchId,
            TotalSystem = SuggestedSystemTotal,
            TotalPhysical = totalPhysical,
            Difference = totalPhysical - SuggestedSystemTotal,
            CashSales = SuggestedCashTotal,
            CardSales = SuggestedCardTotal,
            TransferSales = SuggestedTransferTotal,
            User = name
        };

        db.CashCuts.Add(cut);
        await db.SaveChangesAsync();

        TempData["Flash"] = "Corte registrado.";
        return RedirectToPage(new { branchId = BranchId });
    }

    private async Task LoadTodayTotalsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var sales = db.Sales.Where(x => x.Date >= today && x.Status == "Completed" && x.BranchId == BranchId);

        SuggestedSystemTotal = await sales.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedCashTotal = await sales.Where(x => x.PaymentMethod == "Cash").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedCardTotal = await sales.Where(x => x.PaymentMethod == "Card").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedTransferTotal = await sales.Where(x => x.PaymentMethod == "Transfer").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
    }
}
