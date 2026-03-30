using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Customers;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Customer> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty]
    public Customer Input { get; set; } = new();

    [BindProperty]
    public bool InputRequiresInvoice { get; set; }

    public List<SelectListItem> UsoCfdiOptions { get; private set; } = [];
    public List<SelectListItem> RegimenFiscalOptions { get; private set; } = [];
    public List<SelectListItem> FormaPagoOptions { get; private set; } = [];
    public List<SelectListItem> MetodoPagoOptions { get; private set; } = [];
    public List<SelectListItem> TipoComprobanteOptions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        LoadCatalogs();

        if (string.IsNullOrWhiteSpace(Input.FullName))
        {
            TempData["Flash"] = "Nombre del cliente requerido.";
            return RedirectToPage(new { q = Q });
        }

        if (InputRequiresInvoice)
        {
            var rfc = (Input.Rfc ?? string.Empty).Trim().ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(rfc, @"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$"))
            {
                TempData["Flash"] = "RFC inválido para cliente con factura.";
                return RedirectToPage(new { q = Q });
            }

            var cp = (Input.PostalCode ?? string.Empty).Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(cp, @"^\d{5}$"))
            {
                TempData["Flash"] = "Código postal inválido para factura.";
                return RedirectToPage(new { q = Q });
            }

            Input.Rfc = rfc;
            Input.PostalCode = cp;
            Input.CfdiUse = string.IsNullOrWhiteSpace(Input.CfdiUse) ? "G03" : Input.CfdiUse.Trim().ToUpperInvariant();
            Input.FiscalRegime = string.IsNullOrWhiteSpace(Input.FiscalRegime) ? "601" : Input.FiscalRegime.Trim();
            Input.PaymentMethodSat = string.IsNullOrWhiteSpace(Input.PaymentMethodSat) ? "PUE" : Input.PaymentMethodSat.Trim().ToUpperInvariant();
            Input.PaymentForm = string.IsNullOrWhiteSpace(Input.PaymentForm) ? "01" : Input.PaymentForm.Trim();
            Input.InvoiceType = string.IsNullOrWhiteSpace(Input.InvoiceType) ? "I" : Input.InvoiceType.Trim().ToUpperInvariant();
        }
        else
        {
            Input.Rfc = "XAXX010101000";
            Input.PostalCode = string.Empty;
            Input.CfdiUse = "S01";
            Input.FiscalRegime = "616";
            Input.PaymentMethodSat = "PUE";
            Input.PaymentForm = "01";
            Input.InvoiceType = "I";
        }
        Input.IsActive = true;

        db.Customers.Add(Input);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente guardado.";
        return RedirectToPage(new { q = Q });
    }

    private async Task LoadAsync()
    {
        LoadCatalogs();

        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x => x.FullName.Contains(Q) || x.Rfc.Contains(Q) || x.Email.Contains(Q));
        }

        Items = await query.OrderBy(x => x.FullName).ToListAsync();
    }

    private void LoadCatalogs()
    {
        UsoCfdiOptions = SatCatalogs.UsoCfdi.Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code)).ToList();
        RegimenFiscalOptions = SatCatalogs.RegimenFiscal.Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code)).ToList();
        FormaPagoOptions = SatCatalogs.FormaPago.Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code)).ToList();
        MetodoPagoOptions = SatCatalogs.MetodoPago.Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code)).ToList();
        TipoComprobanteOptions = SatCatalogs.TipoComprobante.Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Code)).ToList();
    }
}
