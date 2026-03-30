using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Reports;

public class DailyModel(AppDbContext db, IUserContextService userContext, IReportPdfService pdfService) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<Sale> Sales { get; private set; } = new();
    public AppConfig Config { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public decimal Total => Sales.Where(x => x.Status == "Completed").Sum(x => x.TotalAmount);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostPdfAsync(int branchId, DateTime date)
    {
        BranchId = branchId;
        Date = date;
        await LoadAsync();

        var branchName = Branches.FirstOrDefault(b => b.Value == BranchId.ToString())?.Text ?? "Sucursal";
        var symbol = string.IsNullOrWhiteSpace(Config.CurrencySymbol) ? "$" : Config.CurrencySymbol;

        var bytes = pdfService.BuildDailySalesPdf(Date, branchName, symbol, Sales);
        var filename = $"reporte-ventas-{Date:yyyyMMdd}-suc-{BranchId}.pdf";

        return File(bytes, "application/pdf", filename);
    }

    private async Task LoadAsync()
    {
        var branchList = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branchList.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();

        Config = await db.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();

        if (Branches.Count == 0)
        {
            Sales = [];
            BranchId = 0;
            return;
        }

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        var start = Date.Date;
        var end = start.AddDays(1);

        Sales = await db.Sales
            .Include(x => x.Customer)
            .Include(x => x.Branch)
            .Where(x => x.BranchId == BranchId && x.Date >= start && x.Date < end)
            .OrderByDescending(x => x.Date)
            .ToListAsync();
    }
}

