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

        Item.Rfc = (Item.Rfc ?? string.Empty).Trim().ToUpperInvariant();
        Item.CfdiUse = string.IsNullOrWhiteSpace(Item.CfdiUse) ? "G03" : Item.CfdiUse.Trim().ToUpperInvariant();
        Item.FiscalRegime = string.IsNullOrWhiteSpace(Item.FiscalRegime) ? "601" : Item.FiscalRegime.Trim();
        Item.PaymentMethodSat = string.IsNullOrWhiteSpace(Item.PaymentMethodSat) ? "PUE" : Item.PaymentMethodSat.Trim().ToUpperInvariant();
        Item.PaymentForm = string.IsNullOrWhiteSpace(Item.PaymentForm) ? "01" : Item.PaymentForm.Trim();
        db.Customers.Add(Item);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente guardado.";
        return RedirectToPage("/Customers/Index");
    }
}
