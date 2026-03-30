using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Product Item { get; set; } = new();

    public List<SelectListItem> Categories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (item is null)
        {
            return RedirectToPage("/Products/Index");
        }

        Item = item;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var current = await db.Products.FirstOrDefaultAsync(p => p.Id == Item.Id);
        if (current is null)
        {
            return RedirectToPage("/Products/Index");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        current.Name = Item.Name.Trim();
        current.Barcode = Item.Barcode.Trim();
        current.CategoryId = Item.CategoryId;
        current.Price = Item.Price;
        current.Cost = Item.Cost;
        current.Stock = Item.Stock;
        current.PriceIncludesTax = Item.PriceIncludesTax;
        current.SatProductCode = Item.SatProductCode.Trim();
        current.SatUnitCode = Item.SatUnitCode.Trim();

        await db.SaveChangesAsync();
        TempData["Flash"] = "Producto actualizado.";
        return RedirectToPage("/Products/Index");
    }

    private async Task LoadAsync()
    {
        Categories = await db.Categories.OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }
}

