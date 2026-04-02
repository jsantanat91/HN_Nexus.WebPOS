using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class MonitoringModel(AppDbContext db, IAlertEmailService email) : PageModel
{
    public List<EndpointStat> SlowEndpoints { get; private set; } = new();
    public List<RecentErrorItem> RecentErrors { get; private set; } = new();

    public long TotalEvents { get; private set; }
    public long ErrorEvents { get; private set; }
    public double AvgMs { get; private set; }
    public double AvgDbMs { get; private set; }
    public double AvgDbCommands { get; private set; }
    public long SlowEvents { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; } = 24;

    public async Task OnGetAsync()
    {
        Hours = Math.Clamp(Hours, 1, 168);
        await EnsureTelemetryColumnsAsync();

        var from = DateTime.UtcNow.AddHours(-Hours);
        var q = db.AppTelemetryEvents.Where(x => x.CreatedAt >= from);

        try
        {
            TotalEvents = await q.LongCountAsync();
            ErrorEvents = await q.LongCountAsync(x => x.StatusCode >= 500);
            SlowEvents = await q.LongCountAsync(x => x.DurationMs >= 1200);
            AvgMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DurationMs) : 0;
            AvgDbMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DbDurationMs) : 0;
            AvgDbCommands = await q.AnyAsync() ? await q.AverageAsync(x => x.DbCommandCount) : 0;

            var events = await q
                .Select(x => new
                {
                    x.Path,
                    x.DurationMs,
                    x.StatusCode,
                    x.DbDurationMs,
                    x.DbCommandCount
                })
                .ToListAsync();

            SlowEndpoints = events
                .GroupBy(x => x.Path)
                .Select(g =>
                {
                    var sorted = g.Select(v => v.DurationMs).OrderBy(v => v).ToList();
                    if (sorted.Count == 0)
                    {
                        return new EndpointStat
                        {
                            Path = g.Key,
                            Calls = 0
                        };
                    }

                    var p95Index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
                    p95Index = Math.Clamp(p95Index, 0, sorted.Count - 1);

                    return new EndpointStat
                    {
                        Path = g.Key,
                        Calls = g.Count(),
                        AvgMs = g.Average(x => (double)x.DurationMs),
                        P95Ms = sorted[p95Index],
                        Errors = g.Count(x => x.StatusCode >= 500),
                        AvgDbMs = g.Average(x => (double)x.DbDurationMs),
                        AvgDbCommands = g.Average(x => (double)x.DbCommandCount)
                    };
                })
                .OrderByDescending(x => x.P95Ms)
                .ThenByDescending(x => x.Calls)
                .Take(20)
                .ToList();

            RecentErrors = await q
                .Where(x => x.StatusCode >= 500 || x.Path == "/Error")
                .OrderByDescending(x => x.CreatedAt)
                .Take(30)
                .Select(x => new RecentErrorItem
                {
                    CreatedAt = x.CreatedAt,
                    Method = x.Method,
                    Path = x.Path,
                    StatusCode = x.StatusCode,
                    IpAddress = x.IpAddress,
                    TenantSchema = x.TenantSchema,
                    TraceId = x.TraceId,
                    Error = x.Error
                })
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42703")
        {
            // Fallback seguro para esquemas legacy aún sin columnas DB.
            TotalEvents = await q.LongCountAsync();
            ErrorEvents = await q.LongCountAsync(x => x.StatusCode >= 500);
            SlowEvents = await q.LongCountAsync(x => x.DurationMs >= 1200);
            AvgMs = await q.AnyAsync() ? await q.AverageAsync(x => (double)x.DurationMs) : 0;
            AvgDbMs = 0;
            AvgDbCommands = 0;

            var basic = await q
                .Select(x => new
                {
                    x.Path,
                    x.DurationMs,
                    x.StatusCode
                })
                .ToListAsync();

            SlowEndpoints = basic
                .GroupBy(x => x.Path)
                .Select(g =>
                {
                    var sorted = g.Select(v => v.DurationMs).OrderBy(v => v).ToList();
                    if (sorted.Count == 0)
                    {
                        return new EndpointStat
                        {
                            Path = g.Key,
                            Calls = 0
                        };
                    }

                    var p95Index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
                    p95Index = Math.Clamp(p95Index, 0, sorted.Count - 1);

                    return new EndpointStat
                    {
                        Path = g.Key,
                        Calls = g.Count(),
                        AvgMs = g.Average(x => (double)x.DurationMs),
                        P95Ms = sorted[p95Index],
                        Errors = g.Count(x => x.StatusCode >= 500),
                        AvgDbMs = 0,
                        AvgDbCommands = 0
                    };
                })
                .OrderByDescending(x => x.P95Ms)
                .ThenByDescending(x => x.Calls)
                .Take(20)
                .ToList();

            RecentErrors = await q
                .Where(x => x.StatusCode >= 500 || x.Path == "/Error")
                .OrderByDescending(x => x.CreatedAt)
                .Take(30)
                .Select(x => new RecentErrorItem
                {
                    CreatedAt = x.CreatedAt,
                    Method = x.Method,
                    Path = x.Path,
                    StatusCode = x.StatusCode,
                    IpAddress = x.IpAddress,
                    TenantSchema = x.TenantSchema,
                    TraceId = x.TraceId,
                    Error = x.Error
                })
                .ToListAsync();
        }
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

    private async Task EnsureTelemetryColumnsAsync()
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "AppTelemetryEvents" ADD COLUMN IF NOT EXISTS "TraceId" text NOT NULL DEFAULT '';
            ALTER TABLE "AppTelemetryEvents" ADD COLUMN IF NOT EXISTS "TenantSchema" text NOT NULL DEFAULT 'public';
            ALTER TABLE "AppTelemetryEvents" ADD COLUMN IF NOT EXISTS "DbCommandCount" integer NOT NULL DEFAULT 0;
            ALTER TABLE "AppTelemetryEvents" ADD COLUMN IF NOT EXISTS "DbDurationMs" bigint NOT NULL DEFAULT 0;
            """);
    }

    public class EndpointStat
    {
        public string Path { get; set; } = string.Empty;
        public int Calls { get; set; }
        public double AvgMs { get; set; }
        public long P95Ms { get; set; }
        public int Errors { get; set; }
        public double AvgDbMs { get; set; }
        public double AvgDbCommands { get; set; }
    }

    public class RecentErrorItem
    {
        public DateTime CreatedAt { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string TenantSchema { get; set; } = "public";
        public string TraceId { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}
