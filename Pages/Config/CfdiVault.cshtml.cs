using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CfdiVaultModel(AppDbContext db, ICfdiVaultService vault) : PageModel
{
    public List<HN_Nexus.WebPOS.Models.CfdiVaultFile> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SaleId { get; set; }

    public async Task OnGetAsync()
    {
        var q = db.CfdiVaultFiles.Include(x => x.Sale).AsQueryable();
        if (SaleId.HasValue && SaleId.Value > 0)
        {
            q = q.Where(x => x.SaleId == SaleId.Value);
        }

        Items = await q.OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        var item = await db.CfdiVaultFiles.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        var file = await vault.ReadAsync(item.StoragePath, item.OriginalFileName);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Value.content, file.Value.contentType, file.Value.fileName);
    }
}
