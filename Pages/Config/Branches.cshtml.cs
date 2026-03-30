using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class BranchesModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Branch NewBranch { get; set; } = new();

    public List<Branch> Branches { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Branches = await db.Branches.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBranch.Name))
        {
            TempData["Flash"] = "Nombre de sucursal requerido.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(NewBranch.Code))
        {
            NewBranch.Code = $"SUC-{DateTime.UtcNow:HHmmss}";
        }

        db.Branches.Add(NewBranch);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Sucursal creada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == id);
        if (branch is null)
        {
            return RedirectToPage();
        }

        branch.IsActive = !branch.IsActive;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Estatus de sucursal actualizado.";
        return RedirectToPage();
    }
}
