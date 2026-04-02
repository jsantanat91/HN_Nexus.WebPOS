using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Services;

public class CfdiStampRetryWorker(IServiceScopeFactory scopeFactory, ILogger<CfdiStampRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en worker de reintento CFDI.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vault = scope.ServiceProvider.GetRequiredService<ICfdiVaultService>();

        var now = DateTime.UtcNow;
        var jobs = await db.CfdiStampQueues
            .Where(x => x.Status == "Pending" && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (jobs.Count == 0)
        {
            return;
        }

        var cfg = await db.AppConfigs.FirstOrDefaultAsync(ct) ?? new AppConfig();
        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            job.Status = "Processing";
            job.LastAttemptAt = DateTime.UtcNow;
            job.Attempts += 1;
            await db.SaveChangesAsync(ct);

            try
            {
                var sale = await db.Sales
                    .Include(x => x.Customer)
                    .Include(x => x.Details)
                    .FirstOrDefaultAsync(x => x.Id == job.SaleId, ct);
                if (sale is null)
                {
                    job.Status = "Error";
                    job.LastError = "Venta no encontrada.";
                    job.NextAttemptAt = null;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var uuid = Guid.NewGuid().ToString().ToUpperInvariant();
                var xmlName = $"cfdi-{sale.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
                var xml = BuildMockCfdiXml(sale, uuid, cfg.CompanyName, cfg.TaxId);
                var xmlBytes = Encoding.UTF8.GetBytes(xml);
                var xmlVault = await vault.SaveAsync("xml", xmlName, xmlBytes);

                var doc = await db.CfdiDocuments.FirstOrDefaultAsync(x => x.SaleId == sale.Id, ct);
                if (doc is null)
                {
                    doc = new CfdiDocument
                    {
                        SaleId = sale.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.CfdiDocuments.Add(doc);
                }

                doc.PacProvider = cfg.PacProvider ?? "Pendiente";
                doc.Status = "Stamped";
                doc.Uuid = uuid;
                doc.XmlPath = xmlVault.storagePath;
                doc.ErrorMessage = null;
                doc.StampedAt = DateTime.UtcNow;
                sale.CfdiStatus = "Timbrado";
                sale.CfdiUuid = uuid;

                db.CfdiVaultFiles.Add(new CfdiVaultFile
                {
                    SaleId = sale.Id,
                    DocumentType = "XML",
                    OriginalFileName = xmlName,
                    StoragePath = xmlVault.storagePath,
                    Sha256 = xmlVault.sha256,
                    SizeBytes = xmlVault.sizeBytes,
                    CreatedAt = DateTime.UtcNow
                });

                job.Status = "Done";
                job.LastError = null;
                job.NextAttemptAt = null;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                job.Status = "Pending";
                job.LastError = ex.Message;
                var delayMinutes = Math.Min(30, Math.Max(2, job.Attempts * 2));
                job.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static string BuildMockCfdiXml(Sale sale, string uuid, string companyName, string rfc)
    {
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<cfdi:Comprobante Version="4.0" Serie="POS" Folio="{sale.Id}" Fecha="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}" SubTotal="{sale.SubtotalAmount:0.00}" Total="{sale.TotalAmount:0.00}" Moneda="MXN" xmlns:cfdi="http://www.sat.gob.mx/cfd/4">
  <cfdi:Emisor Nombre="{companyName}" Rfc="{rfc}" />
  <cfdi:Receptor Nombre="{sale.Customer?.FullName ?? "PUBLICO EN GENERAL"}" Rfc="{sale.Customer?.Rfc ?? "XAXX010101000"}" />
  <cfdi:Complemento>
    <tfd:TimbreFiscalDigital Version="1.1" UUID="{uuid}" FechaTimbrado="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" />
  </cfdi:Complemento>
</cfdi:Comprobante>
""";
    }
}
