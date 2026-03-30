using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CategoriesModel(AppDbContext db) : PageModel
{
    public List<Category> Items { get; private set; } = new();

    [BindProperty]
    public string NewName { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            TempData["Flash"] = "Escribe un nombre de categoría.";
            return RedirectToPage();
        }

        var exists = await db.Categories.AnyAsync(c => c.Name.ToLower() == NewName.Trim().ToLower());
        if (exists)
        {
            TempData["Flash"] = "Esa categoría ya existe.";
            return RedirectToPage();
        }

        db.Categories.Add(new Category { Name = NewName.Trim() });
        await db.SaveChangesAsync();
        TempData["Flash"] = "Categoría creada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string name)
    {
        var item = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (item is null)
        {
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Flash"] = "Nombre no válido.";
            return RedirectToPage();
        }

        item.Name = name.Trim();
        await db.SaveChangesAsync();
        TempData["Flash"] = "Categoría actualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var item = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (item is null)
        {
            return RedirectToPage();
        }

        var used = await db.Products.AnyAsync(p => p.CategoryId == id);
        if (used)
        {
            TempData["Flash"] = "No puedes eliminar una categoría con productos.";
            return RedirectToPage();
        }

        db.Categories.Remove(item);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Categoría eliminada.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await db.Categories.OrderBy(c => c.Name).ToListAsync();
    }
}
