using System.Globalization;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Product> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty]
    public int EditId { get; set; }

    [BindProperty]
    public string EditName { get; set; } = string.Empty;

    [BindProperty]
    public string EditBarcode { get; set; } = string.Empty;

    [BindProperty]
    public decimal EditPrice { get; set; }

    [BindProperty]
    public decimal EditCost { get; set; }

    [BindProperty]
    public int EditStock { get; set; }

    [BindProperty]
    public int EditCategoryId { get; set; }

    [BindProperty]
    public bool EditPriceIncludesTax { get; set; }

    public List<Category> Categories { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public IActionResult OnGetTemplate()
    {
        var csv = "Nombre,CodigoBarras,Categoria,Precio,Costo,Stock,IncluyeIVA\n" +
                  "Producto Demo,2001,General,99.90,60.00,10,SI\n";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "plantilla_productos.csv");
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Flash"] = "Selecciona un archivo para importar.";
            return RedirectToPage();
        }

        var categories = await db.Categories.ToListAsync();
        var activeBranches = await db.Branches.Where(b => b.IsActive).OrderBy(b => b.Id).ToListAsync();
        var defaultBranch = activeBranches.FirstOrDefault();

        var created = 0;
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        _ = await reader.ReadLineAsync();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.None);
            if (parts.Length < 7)
            {
                continue;
            }

            var name = parts[0].Trim();
            var barcode = parts[1].Trim();
            var categoryName = parts[2].Trim();
            _ = decimal.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
            _ = decimal.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var cost);
            _ = int.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var stock);
            var includesTax = parts[6].Trim().Equals("SI", StringComparison.OrdinalIgnoreCase) || parts[6].Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var category = categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = new Category { Name = string.IsNullOrWhiteSpace(categoryName) ? "General" : categoryName };
                db.Categories.Add(category);
                await db.SaveChangesAsync();
                categories.Add(category);
            }

            var nextNumber = (await db.Products.MaxAsync(x => (int?)x.ProductNumber) ?? 0) + 1;
            var product = new Product
            {
                ProductNumber = nextNumber,
                Name = name,
                Barcode = string.IsNullOrWhiteSpace(barcode) ? $"IMP-{nextNumber:D6}" : barcode,
                CategoryId = category.Id,
                Price = price,
                Cost = cost,
                Stock = stock,
                PriceIncludesTax = includesTax,
                SatProductCode = "01010101",
                SatUnitCode = "H87"
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            if (defaultBranch is not null)
            {
                db.ProductStocks.Add(new ProductStock
                {
                    ProductId = product.Id,
                    BranchId = defaultBranch.Id,
                    Stock = stock,
                    MinStock = 5
                });
                await db.SaveChangesAsync();
            }

            created++;
        }

        TempData["Flash"] = $"Importación completada. Productos creados: {created}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == EditId);
        if (product is null)
        {
            return RedirectToPage(new { q = Q });
        }

        product.Name = EditName.Trim();
        product.Barcode = EditBarcode.Trim();
        product.Price = EditPrice;
        product.Cost = EditCost;
        product.Stock = EditStock;
        product.CategoryId = EditCategoryId;
        product.PriceIncludesTax = EditPriceIncludesTax;

        await db.SaveChangesAsync();
        TempData["Flash"] = "Producto actualizado.";
        return RedirectToPage(new { q = Q });
    }

    private async Task LoadAsync()
    {
        Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();

        var query = db.Products.Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x => x.Name.Contains(Q) || x.Barcode.Contains(Q) || x.ProductNumber.ToString().Contains(Q));
        }

        Items = await query.OrderBy(x => x.ProductNumber).ToListAsync();
    }
}

