using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class PromotionsModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<PromotionRule> Items { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        int? branchId,
        string name,
        string ruleType,
        int? productId,
        string? productIdsCsv,
        decimal comboPrice,
        int buyQty,
        int payQty,
        decimal discountPercent,
        string? startTime,
        string? endTime,
        string? daysOfWeekCsv)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Flash"] = "Nombre de promocion requerido.";
            return RedirectToPage(new { branchId = BranchId });
        }

        TimeOnly? start = null;
        TimeOnly? end = null;
        if (!string.IsNullOrWhiteSpace(startTime) && TimeOnly.TryParse(startTime, out var s))
        {
            start = s;
        }
        if (!string.IsNullOrWhiteSpace(endTime) && TimeOnly.TryParse(endTime, out var e))
        {
            end = e;
        }

        if (string.Equals(ruleType, "ComboPrice", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(productIdsCsv))
        {
            TempData["Flash"] = "Para combo captura IDs de productos (ej. 1,2,3).";
            return RedirectToPage(new { branchId = BranchId });
        }

        db.PromotionRules.Add(new PromotionRule
        {
            Name = name.Trim(),
            IsActive = true,
            RuleType = string.IsNullOrWhiteSpace(ruleType) ? "ThreeForTwo" : ruleType,
            BranchId = branchId > 0 ? branchId : null,
            ProductId = productId > 0 ? productId : null,
            ProductIdsCsv = string.IsNullOrWhiteSpace(productIdsCsv) ? null : productIdsCsv.Trim(),
            ComboPrice = comboPrice < 0 ? 0 : comboPrice,
            BuyQty = buyQty <= 0 ? 3 : buyQty,
            PayQty = payQty < 0 ? 2 : payQty,
            DiscountPercent = discountPercent < 0 ? 0 : discountPercent,
            StartTime = start,
            EndTime = end,
            DaysOfWeekCsv = string.IsNullOrWhiteSpace(daysOfWeekCsv) ? null : daysOfWeekCsv.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Promocion guardada.";
        return RedirectToPage(new { branchId = BranchId });
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, int branchId)
    {
        var item = await db.PromotionRules.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return RedirectToPage(new { branchId });
        }

        item.IsActive = !item.IsActive;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Estatus de promocion actualizado.";
        return RedirectToPage(new { branchId });
    }

    private async Task LoadAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToList();

        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Products = await db.Products
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        Items = await db.PromotionRules
            .Include(x => x.Product)
            .Include(x => x.Branch)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
    }
}

