using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class MonitoringModel(AppDbContext db, IAlertEmailService email) : PageModel
{
    public List<EndpointStat> SlowEndpoints { get; private set; } = new();
    public List<HN_Nexus.WebPOS.Models.AppTelemetryEvent> RecentErrors { get; private set; } = new();

    public long TotalEvents { get; private set; }
    public long ErrorEvents { get; private set; }
    public double AvgMs { get; private set; }
    public long SlowEvents { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; } = 24;

    public async Task OnGetAsync()
    {
        Hours = Math.Clamp(Hours, 1, 168);
        var from = DateTime.UtcNow.AddHours(-Hours);

        var q = db.AppTelemetryEvents.Where(x => x.CreatedAt >= from);
        TotalEvents = await q.LongCountAsync();
        ErrorEvents = await q.LongCountAsync(x => x.StatusCode >= 500 || x.Error != null);
        SlowEvents = await q.LongCountAsync(x => x.DurationMs >= 1200);
        AvgMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DurationMs) : 0;

        SlowEndpoints = await q
            .GroupBy(x => x.Path)
            .Select(g => new EndpointStat
            {
                Path = g.Key,
                Calls = g.Count(),
                AvgMs = g.Average(x => (double)x.DurationMs),
                P95Ms = g.OrderBy(x => x.DurationMs).Skip((int)(g.Count() * 0.95)).Select(x => x.DurationMs).FirstOrDefault(),
                Errors = g.Count(x => x.StatusCode >= 500 || x.Error != null)
            })
            .OrderByDescending(x => x.P95Ms)
            .ThenByDescending(x => x.Calls)
            .Take(20)
            .ToListAsync();

        RecentErrors = await q
            .Where(x => x.StatusCode >= 500 || x.Error != null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSendTestEmailAsync()
    {
        var ok = await email.SendAlertAsync(
            "[HN Nexus POS] Prueba de alerta",
            $"Prueba enviada: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        TempData["Flash"] = ok
            ? "Correo de prueba enviado."
            : "No se pudo enviar. Revisa SMTP en Configuración General.";
        return RedirectToPage(new { Hours });
    }

    public class EndpointStat
    {
        public string Path { get; set; } = string.Empty;
        public int Calls { get; set; }
        public double AvgMs { get; set; }
        public long P95Ms { get; set; }
        public int Errors { get; set; }
    }
}
