using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public AppConfig Item { get; set; } = new();

    public async Task OnGetAsync()
    {
        Item = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var current = await db.AppConfigs.FirstOrDefaultAsync();
        Item.TaxId = (Item.TaxId ?? string.Empty).Trim().ToUpperInvariant();
        if (current is null)
        {
            db.AppConfigs.Add(Item);
        }
        else
        {
            current.CompanyName = Item.CompanyName;
            current.TaxId = Item.TaxId;
            current.Address = Item.Address;
            current.Phone = Item.Phone;
            current.CurrencySymbol = Item.CurrencySymbol;
            current.TaxRate = Item.TaxRate;
            current.TicketPrinterName = Item.TicketPrinterName;
            current.TicketHeader = Item.TicketHeader;
            current.TicketFooter = Item.TicketFooter;
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Configuración actualizada.";
        return RedirectToPage();
    }
}
