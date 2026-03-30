using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class NewModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    [BindProperty]
    public Product Item { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();
    public List<SelectListItem> Branches { get; set; } = new();
    public int NextNumber { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Item.ProductNumber = (await db.Products.MaxAsync(x => (int?)x.ProductNumber) ?? 0) + 1;

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.Products.Add(Item);
        await db.SaveChangesAsync();

        db.ProductStocks.Add(new ProductStock
        {
            ProductId = Item.Id,
            BranchId = BranchId,
            Stock = Item.Stock,
            MinStock = 5
        });

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "CREATE",
            Entity = "Product",
            EntityId = Item.Id,
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Alta de producto '{Item.Name}' en sucursal {BranchId}."
        });

        await db.SaveChangesAsync();

        TempData["Flash"] = "Producto guardado en la sucursal seleccionada.";
        return RedirectToPage("/Products/Index", new { branchId = BranchId });
    }

    private async Task LoadAsync()
    {
        var accessible = await userContext.GetAccessibleBranchesAsync(User);
        Branches = accessible
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToList();

        if (Branches.Count > 0 && (BranchId <= 0 || !Branches.Any(b => b.Value == BranchId.ToString())))
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        NextNumber = (await db.Products.MaxAsync(x => (int?)x.ProductNumber) ?? 0) + 1;
        if (Item.ProductNumber <= 0)
        {
            Item.ProductNumber = NextNumber;
        }

        Categories = await db.Categories.OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        if (Item.CategoryId == 0 && Categories.Count > 0)
        {
            Item.CategoryId = int.Parse(Categories[0].Value!);
        }
    }
}
