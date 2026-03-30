using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class AuditModel(AppDbContext db) : PageModel
{
    public List<AuditLog> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        var query = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x => x.Action.Contains(Q) || x.Entity.Contains(Q) || x.Username.Contains(Q) || x.Details.Contains(Q));
        }

        Items = await query.OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync();
    }
}
