using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.CashCuts;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<CashCut> Items { get; private set; } = new();
    public decimal SuggestedSystemTotal { get; private set; }

    public async Task OnGetAsync()
    {
        Items = await db.CashCuts.OrderByDescending(x => x.CutDate).Take(100).ToListAsync();

        var today = DateTime.UtcNow.Date;
        SuggestedSystemTotal = await db.Sales.Where(x => x.Date >= today)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
    }

    public async Task<IActionResult> OnPostCreateAsync(decimal totalPhysical)
    {
        var today = DateTime.UtcNow.Date;
        var totalSystem = await db.Sales.Where(x => x.Date >= today)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var name = User.Identity?.Name ?? User.FindFirstValue("username") ?? "Usuario";
        var cut = new CashCut
        {
            CutDate = DateTime.UtcNow,
            TotalSystem = totalSystem,
            TotalPhysical = totalPhysical,
            Difference = totalPhysical - totalSystem,
            User = name
        };

        db.CashCuts.Add(cut);
        await db.SaveChangesAsync();

        TempData["Flash"] = "Corte registrado.";
        return RedirectToPage();
    }
}
