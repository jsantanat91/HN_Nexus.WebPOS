using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.CashCuts;

public class IndexModel(AppDbContext db, IUserContextService userContext, IWebHostEnvironment env) : PageModel
{
    public List<CashCut> Items { get; private set; } = new();
    public List<CashShift> Shifts { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Historical { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = DateTime.Today;

    public decimal SuggestedSystemTotal { get; private set; }
    public decimal SuggestedCashTotal { get; private set; }
    public decimal SuggestedCardTotal { get; private set; }
    public decimal SuggestedTransferTotal { get; private set; }

    public CashShift? OpenShift { get; private set; }

    public async Task OnGetAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        if (!Historical)
        {
            var nowLocal = DateTime.Now;
            From = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            To = nowLocal.Date;
        }

        var fromUtc = DateTime.SpecifyKind(From.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(To.Date.AddDays(1), DateTimeKind.Utc);

        Items = await db.CashCuts
            .Include(x => x.Branch)
            .Where(x => (BranchId <= 0 || x.BranchId == BranchId) && x.CutDate >= fromUtc && x.CutDate < toUtc)
            .OrderByDescending(x => x.CutDate)
            .Take(300)
            .ToListAsync();

        Shifts = await db.CashShifts
            .Include(x => x.Branch)
            .Include(x => x.User)
            .Where(x => (BranchId <= 0 || x.BranchId == BranchId) && x.OpenedAt >= fromUtc && x.OpenedAt < toUtc)
            .OrderByDescending(x => x.OpenedAt)
            .Take(300)
            .ToListAsync();

        await LoadCurrentShiftAsync();
        await LoadTodayTotalsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(decimal totalPhysical)
    {
        await LoadTodayTotalsAsync();

        var name = User.Identity?.Name ?? User.FindFirstValue("username") ?? "Usuario";
        var cut = new CashCut
        {
            CutDate = DateTime.UtcNow,
            BranchId = BranchId,
            TotalSystem = SuggestedSystemTotal,
            TotalPhysical = totalPhysical,
            Difference = totalPhysical - SuggestedSystemTotal,
            CashSales = SuggestedCashTotal,
            CardSales = SuggestedCardTotal,
            TransferSales = SuggestedTransferTotal,
            User = name
        };

        db.CashCuts.Add(cut);
        await db.SaveChangesAsync();

        TempData["Flash"] = "Corte registrado.";
        return RedirectToPage(new { branchId = BranchId, historical = Historical, from = From.ToString("yyyy-MM-dd"), to = To.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostOpenShiftAsync(int branchId, decimal openingFloat)
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var exists = await db.CashShifts.AnyAsync(x => x.UserId == userId && x.BranchId == branchId && x.Status == "Open");
        if (exists)
        {
            TempData["Flash"] = "Ya tienes un turno abierto en esta sucursal.";
            return RedirectToPage(new { branchId });
        }

        db.CashShifts.Add(new CashShift
        {
            BranchId = branchId,
            UserId = userId.Value,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = openingFloat,
            Status = "Open"
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "OPEN_SHIFT",
            Entity = "CashShift",
            BranchId = branchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = ClientIpResolver.Get(HttpContext),
            Details = $"Apertura de turno con fondo inicial {openingFloat:N2}."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Turno abierto correctamente.";
        return RedirectToPage(new { branchId });
    }

    public async Task<IActionResult> OnPostCloseShiftAsync(int shiftId, int branchId, decimal closingDeclared, string closingSignature, string? closingNotes, IFormFile? closingEvidence)
    {
        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Id == shiftId);
        if (shift is null || shift.Status != "Open")
        {
            return RedirectToPage(new { branchId });
        }

        if (string.IsNullOrWhiteSpace(closingSignature))
        {
            TempData["Flash"] = "Debes capturar el nombre de quien firma el cierre.";
            return RedirectToPage(new { branchId });
        }

        var cashTotal = await db.Sales
            .Where(x => x.BranchId == shift.BranchId && x.Status == "Completed" && x.PaymentMethod == "Cash" && x.Date >= shift.OpenedAt)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        shift.ClosedAt = DateTime.UtcNow;
        shift.ClosingDeclared = closingDeclared;
        shift.SystemCashTotal = shift.OpeningFloat + cashTotal;
        shift.Difference = closingDeclared - shift.SystemCashTotal;
        shift.ClosingSignature = closingSignature.Trim();
        shift.ClosingNotes = (closingNotes ?? string.Empty).Trim();

        if (closingEvidence is not null && closingEvidence.Length > 0)
        {
            var uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "cash-evidence");
            Directory.CreateDirectory(uploadsRoot);
            var ext = Path.GetExtension(closingEvidence.FileName);
            var fileName = $"turno-{shift.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);
            await using var fs = System.IO.File.Create(fullPath);
            await closingEvidence.CopyToAsync(fs);
            shift.ClosingEvidencePath = $"/uploads/cash-evidence/{fileName}";
        }

        shift.Status = "Closed";

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CLOSE_SHIFT",
            Entity = "CashShift",
            EntityId = shift.Id,
            BranchId = shift.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = ClientIpResolver.Get(HttpContext),
            Details = $"Cierre de turno. Declarado={closingDeclared:N2}, Sistema={shift.SystemCashTotal:N2}, Firma={shift.ClosingSignature}."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Turno cerrado con evidencia.";
        return RedirectToPage(new { branchId });
    }

    private async Task LoadCurrentShiftAsync()
    {
        var userId = userContext.GetUserId(User);
        if (userId is null)
        {
            OpenShift = null;
            return;
        }

        OpenShift = await db.CashShifts
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.BranchId == BranchId && x.Status == "Open");
    }

    private async Task LoadTodayTotalsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var sales = db.Sales.Where(x => x.Date >= today && x.Status == "Completed" && x.BranchId == BranchId);

        SuggestedSystemTotal = await sales.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedCashTotal = await sales.Where(x => x.PaymentMethod == "Cash").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedCardTotal = await sales.Where(x => x.PaymentMethod == "Card").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        SuggestedTransferTotal = await sales.Where(x => x.PaymentMethod == "Transfer").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
    }
}
