using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class HealthModel(AppDbContext db) : PageModel
{
    public bool DbOk { get; private set; }
    public long Events24h { get; private set; }
    public long Errors24h { get; private set; }
    public double AvgLatencyMs { get; private set; }
    public double AvgDbMs { get; private set; }
    public double AvgDbCommands { get; private set; }
    public List<AppTelemetryEvent> LastErrors { get; private set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            DbOk = await db.Database.CanConnectAsync();
        }
        catch
        {
            DbOk = false;
        }

        var from = DateTime.UtcNow.AddHours(-24);
        var q = db.AppTelemetryEvents.Where(x => x.CreatedAt >= from);
        Events24h = await q.LongCountAsync();
        Errors24h = await q.LongCountAsync(x => x.StatusCode >= 500 || x.Error != null);
        AvgLatencyMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DurationMs) : 0d;
        AvgDbMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DbDurationMs) : 0d;
        AvgDbCommands = await q.AnyAsync() ? await q.AverageAsync(x => x.DbCommandCount) : 0d;

        LastErrors = await q
            .Where(x => x.StatusCode >= 500 || x.Error != null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync();
    }
}
