using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HN_Nexus.WebPOS.Pages.Customers;

public class NewModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Customer Item { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.Customers.Add(Item);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente guardado.";
        return RedirectToPage("/Customers/Index");
    }
}
