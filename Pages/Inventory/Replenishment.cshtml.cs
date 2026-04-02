using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Inventory;

public class ReplenishmentModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Warehouses { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<ReplenishmentRule> Rules { get; private set; } = new();
    public List<SuggestionRow> Suggestions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int WarehouseId { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostSaveRuleAsync(int branchId, int warehouseId, int productId, int minLevel, int maxLevel, int suggestedOrderQty, bool autoEnabled)
    {
        var rule = await db.ReplenishmentRules.FirstOrDefaultAsync(x => x.BranchId == branchId && x.WarehouseId == warehouseId && x.ProductId == productId);
        if (rule is null)
        {
            rule = new ReplenishmentRule
            {
                BranchId = branchId,
                WarehouseId = warehouseId,
                ProductId = productId
            };
            db.ReplenishmentRules.Add(rule);
        }

        rule.MinLevel = Math.Max(0, minLevel);
        rule.MaxLevel = Math.Max(rule.MinLevel, maxLevel);
        rule.SuggestedOrderQty = Math.Max(0, suggestedOrderQty);
        rule.AutoEnabled = autoEnabled;
        rule.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["Flash"] = "Regla de reabasto guardada.";
        return RedirectToPage(new { branchId, warehouseId });
    }

    private async Task LoadAsync()
    {
        var branches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = branches.OrderBy(x => x.Name).Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Warehouses = await db.Warehouses.Where(x => x.BranchId == BranchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync();
        if (WarehouseId <= 0 && Warehouses.Count > 0)
        {
            WarehouseId = int.Parse(Warehouses[0].Value!);
        }

        Products = await db.WarehouseStocks.Include(x => x.Product)
            .Where(x => x.WarehouseId == WarehouseId)
            .OrderBy(x => x.Product!.Name)
            .Select(x => new SelectListItem(x.Product!.Name, x.ProductId.ToString()))
            .ToListAsync();

        Rules = await db.ReplenishmentRules
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Product)
            .Where(x => x.BranchId == BranchId && x.WarehouseId == WarehouseId)
            .OrderBy(x => x.Product!.Name)
            .ToListAsync();

        var stocks = await db.WarehouseStocks
            .Include(x => x.Product)
            .Where(x => x.WarehouseId == WarehouseId)
            .ToListAsync();

        var ruleMap = Rules.ToDictionary(x => x.ProductId, x => x);
        Suggestions = stocks.Select(s =>
        {
            ruleMap.TryGetValue(s.ProductId, out var r);
            var min = r?.MinLevel ?? s.MinStock;
            var max = r?.MaxLevel ?? Math.Max(min, min + 10);
            var need = s.Stock < min;
            var suggested = need ? Math.Max((r?.SuggestedOrderQty ?? 0), max - s.Stock) : 0;
            return new SuggestionRow
            {
                ProductId = s.ProductId,
                ProductName = s.Product?.Name ?? "Producto",
                CurrentStock = s.Stock,
                MinLevel = min,
                MaxLevel = max,
                SuggestedQty = suggested,
                NeedsReplenishment = need,
                AutoEnabled = r?.AutoEnabled ?? false
            };
        })
        .OrderByDescending(x => x.NeedsReplenishment)
        .ThenBy(x => x.CurrentStock)
        .ToList();
    }

    public class SuggestionRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public int SuggestedQty { get; set; }
        public bool NeedsReplenishment { get; set; }
        public bool AutoEnabled { get; set; }
    }
}
