using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class TicketModel(AppDbContext db) : PageModel
{
    public Sale? Sale { get; private set; }
    public AppConfig Config { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Sale = await db.Sales
            .Include(x => x.Details).ThenInclude(x => x.Product)
             .Include(x => x.Customer)
            .Include(x => x.Branch)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Sale is null)
        {
            return NotFound();
        }

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        return Page();
    }
}



