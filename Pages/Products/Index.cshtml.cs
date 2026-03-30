using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Product> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        var query = db.Products.Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x => x.Name.Contains(Q) || x.Barcode.Contains(Q));
        }

        Items = await query.OrderBy(x => x.Name).ToListAsync();
    }
}
