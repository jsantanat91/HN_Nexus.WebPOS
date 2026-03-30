using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class IndexModel(AppDbContext db, IUserContextService userContext, IReportPdfService pdfService) : PageModel
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
        }

        sale.Status = "Cancelled";
        sale.CancelledAt = DateTime.UtcNow;
        sale.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelada desde historial" : reason.Trim();

        await db.SaveChangesAsync();
        TempData["Flash"] = $"Venta #{sale.Id} cancelada y stock restaurado.";
        return RedirectToPage(new { branchId = sale.BranchId });
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
