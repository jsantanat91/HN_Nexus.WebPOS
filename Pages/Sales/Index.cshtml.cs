using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class IndexModel(AppDbContext db, IUserContextService userContext, IReportPdfService pdfService, IWebHostEnvironment env) : PageModel
{
    public List<Sale> Items { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public async Task OnGetAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var query = db.Sales
            .Include(x => x.User)
            .Include(x => x.Customer)
            .Include(x => x.Branch)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var allowedIds = allowedBranches.Select(b => b.Id).ToList();
            query = query.Where(x => allowedIds.Contains(x.BranchId));
        }

        if (BranchId > 0)
        {
            query = query.Where(x => x.BranchId == BranchId);
        }

        Items = await query
            .OrderByDescending(x => x.Date)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostExportAsync(int branchId, DateTime date, string format)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch is null)
        {
            TempData["Flash"] = "Sucursal no válida.";
            return RedirectToPage(new { branchId });
        }

        var start = date.Date;
        var end = start.AddDays(1);
        var sales = await db.Sales
            .Include(x => x.Customer)
            .Where(x => x.BranchId == branchId && x.Date >= start && x.Date < end)
            .OrderByDescending(x => x.Date)
            .ToListAsync();

        var config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        var symbol = string.IsNullOrWhiteSpace(config.CurrencySymbol) ? "$" : config.CurrencySymbol;

        if (string.Equals(format, "ticket", StringComparison.OrdinalIgnoreCase))
        {
            var html = BuildDailyTicketHtml(date, branch.Name, symbol, sales);
            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"resumen-dia-{date:yyyyMMdd}-ticket.html");
        }

        var pdf = pdfService.BuildDailySalesPdf(date, branch.Name, symbol, sales);
        return File(pdf, "application/pdf", $"reporte-ventas-{date:yyyyMMdd}.pdf");
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason, int branchId)
    {
        var sale = await db.Sales
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale is null)
        {
            return RedirectToPage(new { branchId });
        }

        if (!string.Equals(sale.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash"] = "Solo se pueden cancelar ventas completadas.";
            return RedirectToPage(new { branchId });
        }

        var productIds = sale.Details.Select(d => d.ProductId).ToList();
        var branchStocks = await db.ProductStocks
            .Where(ps => ps.BranchId == sale.BranchId && productIds.Contains(ps.ProductId))
            .ToDictionaryAsync(ps => ps.ProductId);

        foreach (var detail in sale.Details)
        {
            if (branchStocks.TryGetValue(detail.ProductId, out var stock))
            {
                stock.Stock += detail.Quantity;
            }

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == detail.ProductId);
            if (product is not null)
            {
                product.Stock += detail.Quantity;
            }

            var lot = await db.ProductLots
                .Where(x => x.BranchId == sale.BranchId && x.ProductId == detail.ProductId)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();

            if (lot is null)
            {
                db.ProductLots.Add(new ProductLot
                {
                    BranchId = sale.BranchId,
                    ProductId = detail.ProductId,
                    LotNumber = $"DEV-{sale.Id}",
                    SerialNumber = null,
                    ExpirationDate = null,
                    Quantity = detail.Quantity,
                    UnitCost = detail.UnitPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                lot.Quantity += detail.Quantity;
                lot.UpdatedAt = DateTime.UtcNow;
            }
        }

        sale.Status = "Cancelled";
        sale.CancelledAt = DateTime.UtcNow;
        sale.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelada desde historial" : reason.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CANCEL",
            Entity = "Sale",
            EntityId = sale.Id,
            BranchId = sale.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Venta cancelada. Razón: {sale.CancelReason}"
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = $"Venta #{sale.Id} cancelada y stock restaurado.";
        return RedirectToPage(new { branchId = sale.BranchId });
    }

    public async Task<IActionResult> OnPostStampCfdiAsync(int id, int branchId)
    {
        var sale = await db.Sales
            .Include(x => x.Customer)
            .Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (sale is null)
        {
            return RedirectToPage(new { branchId });
        }

        if (!sale.IsInvoice)
        {
            TempData["Flash"] = "Solo aplica timbrado para ventas con factura.";
            return RedirectToPage(new { branchId });
        }

        if (!string.Equals(sale.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash"] = "Solo se pueden timbrar ventas completadas.";
            return RedirectToPage(new { branchId });
        }

        if (sale.CfdiStatus == "Timbrado")
        {
            TempData["Flash"] = "La venta ya está timbrada.";
            return RedirectToPage(new { branchId });
        }

        var config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        if (string.IsNullOrWhiteSpace(config.PacProvider))
        {
            TempData["Flash"] = "Configura proveedor PAC antes de timbrar (Configuración > CFDI/PAC).";
            return RedirectToPage(new { branchId });
        }

        var uuid = Guid.NewGuid().ToString().ToUpperInvariant();
        var cfdiRoot = Path.Combine(env.WebRootPath, "cfdi");
        Directory.CreateDirectory(cfdiRoot);
        var xmlName = $"cfdi-{sale.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
        var xmlPath = Path.Combine(cfdiRoot, xmlName);
        var xml = BuildMockCfdiXml(sale, uuid, config.CompanyName, config.TaxId);
        await System.IO.File.WriteAllTextAsync(xmlPath, xml);

        var doc = await db.CfdiDocuments.FirstOrDefaultAsync(x => x.SaleId == sale.Id);
        if (doc is null)
        {
            doc = new CfdiDocument
            {
                SaleId = sale.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.CfdiDocuments.Add(doc);
        }

        doc.PacProvider = config.PacProvider ?? "Pendiente";
        doc.Status = "Stamped";
        doc.Uuid = uuid;
        doc.XmlPath = $"/cfdi/{xmlName}";
        doc.PdfPath = null;
        doc.ErrorMessage = null;
        doc.StampedAt = DateTime.UtcNow;
        sale.CfdiStatus = "Timbrado";
        sale.CfdiUuid = uuid;

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CFDI_STAMP",
            Entity = "Sale",
            EntityId = sale.Id,
            BranchId = sale.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Timbrado CFDI (modo integración base) UUID={uuid}, PAC={doc.PacProvider}."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = $"CFDI timbrado (base) UUID {uuid}.";
        return RedirectToPage(new { branchId = sale.BranchId });
    }

    public async Task<IActionResult> OnPostCancelCfdiAsync(int id, int branchId, string? reason)
    {
        var sale = await db.Sales.FirstOrDefaultAsync(x => x.Id == id);
        if (sale is null)
        {
            return RedirectToPage(new { branchId });
        }

        var doc = await db.CfdiDocuments.FirstOrDefaultAsync(x => x.SaleId == sale.Id);
        if (doc is null || doc.Status != "Stamped")
        {
            TempData["Flash"] = "No hay CFDI timbrado para cancelar.";
            return RedirectToPage(new { branchId = sale.BranchId });
        }

        doc.Status = "Cancelled";
        doc.CancelledAt = DateTime.UtcNow;
        doc.ErrorMessage = string.IsNullOrWhiteSpace(reason) ? "Cancelado desde sistema" : reason.Trim();
        sale.CfdiStatus = "Cancelado";

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CFDI_CANCEL",
            Entity = "Sale",
            EntityId = sale.Id,
            BranchId = sale.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"CFDI cancelado. UUID={doc.Uuid}. Motivo={doc.ErrorMessage}"
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = $"CFDI de venta #{sale.Id} cancelado.";
        return RedirectToPage(new { branchId = sale.BranchId });
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

    private static string BuildDailyTicketHtml(DateTime date, string branchName, string symbol, List<Sale> sales)
    {
        var completed = sales.Where(s => s.Status == "Completed").ToList();
        var total = completed.Sum(s => s.TotalAmount);

        var rows = string.Join("", completed.Select(s =>
            $"<tr><td>{s.Date.ToLocalTime():HH:mm}</td><td>#{s.Id}</td><td>{(s.Customer?.FullName ?? "Publico General")}</td><td>{s.PaymentMethod}</td><td>{symbol}{s.TotalAmount:N2}</td></tr>"));

        return $@"<!doctype html>
<html lang='es'>
<head>
<meta charset='utf-8' />
<title>Resumen diario ticket</title>
<style>
body {{ font-family: Arial, sans-serif; margin: 12px; }}
h2,h3,p {{ margin: 4px 0; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 8px; }}
th,td {{ border-bottom: 1px dashed #999; text-align: left; padding: 4px; font-size: 12px; }}
.total {{ font-weight: bold; margin-top: 8px; font-size: 14px; }}
</style>
</head>
<body>
<h2>HN Nexus POS</h2>
<p>Resumen diario {date:dd/MM/yyyy}</p>
<p>Sucursal: {branchName}</p>
<p>Ventas: {completed.Count}</p>
<p class='total'>Total: {symbol}{total:N2}</p>
<table>
<thead><tr><th>Hora</th><th>Folio</th><th>Cliente</th><th>Pago</th><th>Total</th></tr></thead>
<tbody>{rows}</tbody>
</table>
<script>window.onload=function(){{window.print();}};</script>
</body>
</html>";
    }
}

