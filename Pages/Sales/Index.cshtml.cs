using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Sale> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await db.Sales
            .Include(x => x.User)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.Date)
            .Take(200)
            .ToListAsync();
    }
}
