using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Customers;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Customer> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x => x.FullName.Contains(Q) || x.Rfc.Contains(Q));
        }

        Items = await query.OrderBy(x => x.FullName).ToListAsync();
    }
}

