using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class WarehousesModel(AppDbContext db) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<Warehouse> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(int branchId, string code, string name)
    {
        if (branchId <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["Flash"] = "Captura sucursal, código y nombre del almacén.";
            return RedirectToPage(new { branchId });
        }

        code = code.Trim().ToUpperInvariant();
        var exists = await db.Warehouses.AnyAsync(x => x.BranchId == branchId && x.Code == code);
        if (exists)
        {
            TempData["Flash"] = "El código de almacén ya existe en la sucursal.";
            return RedirectToPage(new { branchId });
        }

        db.Warehouses.Add(new Warehouse
        {
            BranchId = branchId,
            Code = code,
            Name = name.Trim(),
            IsActive = true
        });
        await db.SaveChangesAsync();
        TempData["Flash"] = "Almacén creado.";
        return RedirectToPage(new { branchId });
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, int branchId)
    {
        var item = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return RedirectToPage(new { branchId });
        }

        item.IsActive = !item.IsActive;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Estatus de almacén actualizado.";
        return RedirectToPage(new { branchId });
    }

    private async Task LoadAsync()
    {
        Branches = await db.Branches
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Items = await db.Warehouses
            .Include(x => x.Branch)
            .Where(x => BranchId <= 0 || x.BranchId == BranchId)
            .OrderBy(x => x.Branch!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }
}

