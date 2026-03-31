using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class PricingModel(AppDbContext db) : PageModel
{
    public List<PriceList> Lists { get; private set; } = new();
    public List<PriceListItem> Items { get; private set; } = new();
    public List<SelectListItem> Products { get; private set; } = new();
    public List<SelectListItem> Customers { get; private set; } = new();
    public List<SelectListItem> PriceListsOptions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PriceListId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateListAsync(string name, bool isWholesale)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Flash"] = "Nombre de lista requerido.";
            return RedirectToPage(new { priceListId = PriceListId });
        }

        db.PriceLists.Add(new PriceList
        {
            Name = name.Trim(),
            IsWholesale = isWholesale,
            IsActive = true
        });
        await db.SaveChangesAsync();
        TempData["Flash"] = "Lista de precio creada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddItemAsync(int priceListId, int productId, int minQty, decimal price)
    {
        if (priceListId <= 0 || productId <= 0 || minQty <= 0 || price <= 0)
        {
            TempData["Flash"] = "Captura datos válidos para regla de precio.";
            return RedirectToPage(new { priceListId = PriceListId });
        }

        var exists = await db.PriceListItems.FirstOrDefaultAsync(x => x.PriceListId == priceListId && x.ProductId == productId && x.MinQty == minQty);
        if (exists is null)
        {
            db.PriceListItems.Add(new PriceListItem
            {
                PriceListId = priceListId,
                ProductId = productId,
                MinQty = minQty,
                Price = price
            });
        }
        else
        {
            exists.Price = price;
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Precio de lista guardado.";
        return RedirectToPage(new { priceListId });
    }

    public async Task<IActionResult> OnPostAssignCustomerAsync(int customerId, int? priceListId)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId);
        if (customer is null)
        {
            return RedirectToPage(new { priceListId = PriceListId });
        }

        customer.PriceListId = priceListId > 0 ? priceListId : null;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Lista asignada al cliente.";
        return RedirectToPage(new { priceListId = PriceListId });
    }

    private async Task LoadAsync()
    {
        Lists = await db.PriceLists.OrderBy(x => x.Name).ToListAsync();
        if (PriceListId <= 0 && Lists.Count > 0)
        {
            PriceListId = Lists[0].Id;
        }

        Products = await db.Products.OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        Customers = await db.Customers.Where(x => x.IsActive).OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();

        PriceListsOptions = Lists.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();

        Items = await db.PriceListItems
            .Include(x => x.Product)
            .Include(x => x.PriceList)
            .Where(x => PriceListId <= 0 || x.PriceListId == PriceListId)
            .OrderBy(x => x.PriceList!.Name)
            .ThenBy(x => x.Product!.Name)
            .ThenBy(x => x.MinQty)
            .ToListAsync();
    }
}

