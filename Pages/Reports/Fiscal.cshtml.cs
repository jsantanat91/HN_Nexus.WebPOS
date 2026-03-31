using System.Globalization;
using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Reports;

public class FiscalModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<Sale> Sales { get; private set; } = new();
    public AppConfig Config { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool AllBranches { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = DateTime.Today;

    public decimal TotalSales => Sales.Sum(x => x.SubtotalAmount);
    public decimal TotalVat => Sales.Sum(x => x.TaxAmount);
    public decimal TotalIncome => Sales.Sum(x => x.TotalAmount);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostExportAsync(string format, int branchId, bool allBranches, DateTime from, DateTime to)
    {
        BranchId = branchId;
        AllBranches = allBranches;
        From = from;
        To = to;
        await LoadAsync();

        format = (format ?? "contpaqi").Trim().ToLowerInvariant();
        var text = format == "coi" ? BuildCoiText() : BuildContpaqiCsv();
        var ext = format == "coi" ? "txt" : "csv";
        return File(Encoding.UTF8.GetBytes(text), "text/plain", $"poliza-fiscal-{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}");
    }

    private async Task LoadAsync()
    {
        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();

        if (!AllBranches && Branches.Count > 0 && (BranchId <= 0 || !Branches.Any(x => x.Value == BranchId.ToString())))
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var allowed = Branches.Select(x => int.Parse(x.Value!)).ToList();
        var start = DateTime.SpecifyKind(From.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(To.Date.AddDays(1), DateTimeKind.Utc);

        var q = db.Sales
            .Include(x => x.Branch)
            .Where(x => x.Status == "Completed" && x.Date >= start && x.Date < end && allowed.Contains(x.BranchId));

        if (!AllBranches)
        {
            q = q.Where(x => x.BranchId == BranchId);
        }

        Sales = await q.OrderBy(x => x.Date).ToListAsync();
    }

    private string BuildContpaqiCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Cuenta,Concepto,Cargo,Abono,Referencia,Sucursal");

        foreach (var sale in Sales)
        {
            var d = sale.Date.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var refx = $"VENTA-{sale.Id}";
            var branch = sale.Branch?.Code ?? "NA";
            var cashAccount = sale.PaymentMethod == "Card" ? Config.CardAccount :
                sale.PaymentMethod == "Transfer" ? Config.TransferAccount : Config.CashAccount;

            sb.AppendLine($"{d},{cashAccount},Cobro venta {sale.Id},{sale.TotalAmount:0.00},0.00,{refx},{branch}");
            sb.AppendLine($"{d},{Config.SalesAccount},Venta neta,{0.00:0.00},{sale.SubtotalAmount:0.00},{refx},{branch}");
            if (sale.TaxAmount > 0)
            {
                sb.AppendLine($"{d},{Config.VatAccount},IVA trasladado,{0.00:0.00},{sale.TaxAmount:0.00},{refx},{branch}");
            }
        }

        return sb.ToString();
    }

    private string BuildCoiText()
    {
        var sb = new StringBuilder();
        foreach (var sale in Sales)
        {
            var d = sale.Date.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var refx = $"VENTA-{sale.Id}";
            var cashAccount = sale.PaymentMethod == "Card" ? Config.CardAccount :
                sale.PaymentMethod == "Transfer" ? Config.TransferAccount : Config.CashAccount;

            sb.AppendLine($"{d}|{cashAccount}|{sale.TotalAmount:0.00}|0.00|Cobro venta {sale.Id}|{refx}");
            sb.AppendLine($"{d}|{Config.SalesAccount}|0.00|{sale.SubtotalAmount:0.00}|Venta neta|{refx}");
            if (sale.TaxAmount > 0)
            {
                sb.AppendLine($"{d}|{Config.VatAccount}|0.00|{sale.TaxAmount:0.00}|IVA trasladado|{refx}");
            }
        }

        return sb.ToString();
    }
}

