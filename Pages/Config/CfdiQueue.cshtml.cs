using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CfdiQueueModel(AppDbContext db) : PageModel
{
    public List<CfdiStampQueue> Jobs { get; private set; } = new();
    public int Pending { get; private set; }
    public int Error { get; private set; }
    public int Done { get; private set; }

    public async Task OnGetAsync()
    {
        Jobs = await db.CfdiStampQueues.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();
        Pending = Jobs.Count(x => x.Status == "Pending" || x.Status == "Processing");
        Error = Jobs.Count(x => x.Status == "Error");
        Done = Jobs.Count(x => x.Status == "Done");
    }

    public async Task<IActionResult> OnPostRetryAsync(long id)
    {
        var job = await db.CfdiStampQueues.FirstOrDefaultAsync(x => x.Id == id);
        if (job is null)
        {
            return RedirectToPage();
        }

        job.Status = "Pending";
        job.NextAttemptAt = DateTime.UtcNow;
        job.LastError = null;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Reintento programado para el job CFDI.";
        return RedirectToPage();
    }
}
