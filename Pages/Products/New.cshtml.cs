using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class NewModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Product Item { get; set; } = new();

    public List<SelectListItem> Categories { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.Products.Add(Item);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Producto guardado.";
        return RedirectToPage("/Products/Index");
    }

    private async Task LoadAsync()
    {
        Categories = await db.Categories.OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        if (Item.CategoryId == 0 && Categories.Count > 0)
        {
            Item.CategoryId = int.Parse(Categories[0].Value!);
        }
    }
}
