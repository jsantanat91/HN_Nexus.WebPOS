using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CfdiModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public AppConfig Item { get; set; } = new();

    public async Task OnGetAsync()
    {
        Item = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var current = await db.AppConfigs.FirstOrDefaultAsync();
        if (current is null)
        {
            current = Item;
            db.AppConfigs.Add(current);
        }
        else
        {
            current.PacProvider = Item.PacProvider;
            current.PacApiUrl = Item.PacApiUrl;
            current.PacApiKey = Item.PacApiKey;
            current.PacApiSecret = Item.PacApiSecret;
            current.PacTestMode = Item.PacTestMode;
            current.CerPath = Item.CerPath;
            current.KeyPath = Item.KeyPath;
            current.PrivateKeyPassword = Item.PrivateKeyPassword;
            current.Street = Item.Street;
            current.PostalCode = Item.PostalCode;
            current.State = Item.State;
            current.Country = Item.Country;
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Configuración CFDI/PAC actualizada.";
        return RedirectToPage();
    }
}

