using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class TicketModel(AppDbContext db, IAlertEmailService emailService) : PageModel
{
    public Sale? Sale { get; private set; }
    public AppConfig Config { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool Embed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Sale = await db.Sales
            .Include(x => x.Details).ThenInclude(x => x.Product)
            .Include(x => x.Customer)
            .Include(x => x.Branch)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Sale is null)
        {
            return NotFound();
        }

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        return Page();
    }

    public async Task<IActionResult> OnPostSendDigitalAsync(int id, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new JsonResult(new { ok = false, message = "Correo requerido." });
        }

        Sale = await db.Sales
            .Include(x => x.Details).ThenInclude(x => x.Product)
            .Include(x => x.Customer)
            .Include(x => x.Branch)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Sale is null)
        {
            return new JsonResult(new { ok = false, message = "Venta no encontrada." });
        }

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        var subject = $"Ticket digital venta #{Sale.Id} - {Config.CompanyName}";
        var body = BuildDigitalTicketHtml(Sale, Config);
        var sent = await emailService.SendToAsync(email.Trim(), subject, body, isHtml: true);
        if (!sent)
        {
            return new JsonResult(new { ok = false, message = "No se pudo enviar. Revisa SMTP en Configuración > General." });
        }

        return new JsonResult(new { ok = true, message = $"Ticket digital enviado a {email.Trim()}." });
    }

    private static string BuildDigitalTicketHtml(Sale sale, AppConfig cfg)
    {
        var symbol = string.IsNullOrWhiteSpace(cfg.CurrencySymbol) ? "$" : cfg.CurrencySymbol;
        var sb = new StringBuilder();
        foreach (var d in sale.Details)
        {
            sb.Append($"<tr><td>{d.Product?.Name} x {d.Quantity}</td><td style='text-align:right'>{symbol}{d.Total:N2}</td></tr>");
        }

        return $"""
<!doctype html>
<html lang=\"es\">
<head><meta charset=\"utf-8\" /><title>Ticket digital</title></head>
<body style=\"font-family:Segoe UI,Arial,sans-serif;color:#0f172a;\">
  <h2 style=\"margin:0;\">{cfg.CompanyName}</h2>
  <p style=\"margin:6px 0;\">Ticket digital venta #{sale.Id}</p>
  <p style=\"margin:6px 0;\">Fecha: {sale.Date.ToLocalTime():dd/MM/yyyy HH:mm}</p>
  <p style=\"margin:6px 0;\">Sucursal: {sale.Branch?.Name}</p>
  <p style=\"margin:6px 0;\">Cliente: {(sale.Customer?.FullName ?? "Publico General")}</p>
  <hr />
  <table style=\"width:100%;border-collapse:collapse;\">{sb}</table>
  <hr />
  <p style=\"margin:4px 0;\">Subtotal: <strong>{symbol}{sale.SubtotalAmount:N2}</strong></p>
  <p style=\"margin:4px 0;\">Descuento: <strong>{symbol}{sale.DiscountAmount:N2}</strong></p>
  <p style=\"margin:4px 0;\">IVA: <strong>{symbol}{sale.TaxAmount:N2}</strong></p>
  <p style=\"margin:8px 0;font-size:18px;\">TOTAL: <strong>{symbol}{sale.TotalAmount:N2}</strong></p>
  <p style=\"margin-top:14px;\">Gracias por su compra.</p>
</body>
</html>
""";
    }
}
