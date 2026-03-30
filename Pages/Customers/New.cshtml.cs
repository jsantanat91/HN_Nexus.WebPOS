using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HN_Nexus.WebPOS.Pages.Customers;

public class NewModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Customer Item { get; set; } = new();

    public List<SelectListItem> UsoCfdiOptions { get; private set; } = [];
    public List<SelectListItem> RegimenFiscalOptions { get; private set; } = [];
    public List<SelectListItem> FormaPagoOptions { get; private set; } = [];
    public List<SelectListItem> MetodoPagoOptions { get; private set; } = [];
    public List<SelectListItem> TipoComprobanteOptions { get; private set; } = [];

    public IActionResult OnGet()
    {
        LoadCatalogs();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadCatalogs();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Item.Rfc = (Item.Rfc ?? string.Empty).Trim().ToUpperInvariant();
        Item.CfdiUse = string.IsNullOrWhiteSpace(Item.CfdiUse) ? "G03" : Item.CfdiUse.Trim().ToUpperInvariant();
        Item.FiscalRegime = string.IsNullOrWhiteSpace(Item.FiscalRegime) ? "601" : Item.FiscalRegime.Trim();
        Item.PaymentMethodSat = string.IsNullOrWhiteSpace(Item.PaymentMethodSat) ? "PUE" : Item.PaymentMethodSat.Trim().ToUpperInvariant();
        Item.PaymentForm = string.IsNullOrWhiteSpace(Item.PaymentForm) ? "01" : Item.PaymentForm.Trim();

        db.Customers.Add(Item);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Cliente guardado.";
        return RedirectToPage("/Customers/Index");
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

