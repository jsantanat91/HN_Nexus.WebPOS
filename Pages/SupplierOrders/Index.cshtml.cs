using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.SupplierOrders;

public class IndexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SupplierOrder> Items { get; private set; } = new();
    public List<SelectListItem> Suppliers { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(int branchId, int supplierId, int productId, int quantity, decimal unitCost)
    {
        BranchId = branchId;
        if (supplierId <= 0 || productId <= 0 || quantity <= 0 || branchId <= 0)
        {
            TempData["Flash"] = "Captura datos válidos para el pedido.";
            return RedirectToPage(new { branchId = BranchId });
        }

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        var stockRow = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.ProductId == productId && ps.BranchId == branchId);

        if (supplier is null || product is null || stockRow is null)
        {
            TempData["Flash"] = "Proveedor o producto inválido para la sucursal seleccionada.";
            return RedirectToPage(new { branchId = BranchId });
        }

        db.SupplierOrders.Add(new SupplierOrder
        {
            SupplierId = supplierId,
            ProductId = productId,
            BranchId = branchId,
            Quantity = quantity,
            UnitCost = unitCost < 0 ? 0 : unitCost,
            Status = "Pendiente",
            OrderDate = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Pedido registrado.";
        return RedirectToPage(new { branchId = BranchId });
    }

    public async Task<IActionResult> OnPostMarkReceivedAsync(int id, int branchId)
    {
        BranchId = branchId;
        var order = await db.SupplierOrders.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (order is null)
        {
            return RedirectToPage(new { branchId = BranchId });
        }

        order.Status = "Recibido";

        var stock = await db.ProductStocks.FirstOrDefaultAsync(x => x.ProductId == order.ProductId && x.BranchId == branchId);
        if (stock is null)
        {
            stock = new ProductStock
            {
                ProductId = order.ProductId,
                BranchId = branchId,
                Stock = order.Quantity,
                MinStock = 5
            };
            db.ProductStocks.Add(stock);
        }
        else
        {
            stock.Stock += order.Quantity;
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == order.ProductId);
        if (product is not null)
        {
            product.Stock += order.Quantity;

            if (order.UnitCost > 0)
            {
                product.Cost = order.UnitCost;
            }
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Pedido marcado como recibido.";
        return RedirectToPage(new { branchId = BranchId });
    }

    private async Task LoadAsync()
    {
        var allowed = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowed
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToList();

        if (Branches.Count > 0 && (BranchId <= 0 || !Branches.Any(b => b.Value == BranchId.ToString())))
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Suppliers = await db.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();

        Products = await db.ProductStocks
            .Include(ps => ps.Product)
            .Where(ps => ps.BranchId == BranchId)
            .OrderBy(ps => ps.Product!.Name)
            .Select(ps => new SelectListItem($"{ps.Product!.Name} (Stock {ps.Stock})", ps.ProductId.ToString()))
            .ToListAsync();

        Items = await db.SupplierOrders
            .Include(x => x.Supplier)
            .Include(x => x.Product)
            .Include(x => x.Branch)
            .Where(x => BranchId <= 0 || x.BranchId == BranchId)
            .OrderByDescending(x => x.OrderDate)
            .Take(200)
            .ToListAsync();
    }
}
