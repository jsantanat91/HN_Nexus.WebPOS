using System.Globalization;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Products;

public class IndexModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<ProductBranchRow> Items { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

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
    public int EditMinStock { get; set; }

    [BindProperty]
    public int EditCategoryId { get; set; }

    [BindProperty]
    public bool EditPriceIncludesTax { get; set; }

    [BindProperty]
    public string EditPromotionType { get; set; } = "None";

    [BindProperty]
    public decimal EditPromotionValue { get; set; }

    [BindProperty]
    public int EditPromotionMinQty { get; set; }

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

    public async Task<IActionResult> OnPostImportAsync(IFormFile? file, int branchId)
    {
        BranchId = branchId;
        if (file is null || file.Length == 0)
        {
            TempData["Flash"] = "Selecciona un archivo para importar.";
            return RedirectToPage(new { branchId = BranchId, q = Q });
        }

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == BranchId && b.IsActive);
        if (branch is null)
        {
            TempData["Flash"] = "Sucursal inválida para importar.";
            return RedirectToPage(new { branchId = BranchId, q = Q });
        }

        var categories = await db.Categories.ToListAsync();

        var created = 0;
        var assigned = 0;
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

            var existing = await db.Products.FirstOrDefaultAsync(p => !string.IsNullOrWhiteSpace(barcode) && p.Barcode == barcode);
            if (existing is not null)
            {
                var existingStock = await db.ProductStocks.FirstOrDefaultAsync(ps => ps.ProductId == existing.Id && ps.BranchId == branch.Id);
                if (existingStock is null)
                {
                    db.ProductStocks.Add(new ProductStock
                    {
                        ProductId = existing.Id,
                        BranchId = branch.Id,
                        Stock = stock,
                        MinStock = 5
                    });
                    assigned++;
                }
                else
                {
                    existingStock.Stock += stock;
                }

                await db.SaveChangesAsync();
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

            db.ProductStocks.Add(new ProductStock
            {
                ProductId = product.Id,
                BranchId = branch.Id,
                Stock = stock,
                MinStock = 5
            });
            await db.SaveChangesAsync();

            created++;
        }

        TempData["Flash"] = $"Importación completada en sucursal. Nuevos: {created}, asignados: {assigned}.";

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "IMPORT",
            Entity = "Product",
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Importación CSV en sucursal {BranchId}. Nuevos={created}, asignados={assigned}."
        });
        await db.SaveChangesAsync();

        return RedirectToPage(new { branchId = BranchId, q = Q });
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == EditId);
        if (product is null)
        {
            return RedirectToPage(new { q = Q, branchId = BranchId });
        }

        var stock = await db.ProductStocks.FirstOrDefaultAsync(x => x.ProductId == EditId && x.BranchId == BranchId);
        if (stock is null)
        {
            stock = new ProductStock
            {
                ProductId = EditId,
                BranchId = BranchId,
                Stock = EditStock,
                MinStock = Math.Max(0, EditMinStock)
            };
            db.ProductStocks.Add(stock);
        }
        else
        {
            stock.Stock = EditStock;
            stock.MinStock = Math.Max(0, EditMinStock);
        }

        product.Name = EditName.Trim();
        product.Barcode = EditBarcode.Trim();
        product.Price = EditPrice;
        product.Cost = EditCost;
        product.CategoryId = EditCategoryId;
        product.PriceIncludesTax = EditPriceIncludesTax;
        product.PromotionType = string.IsNullOrWhiteSpace(EditPromotionType) ? "None" : EditPromotionType;
        product.PromotionValue = Math.Max(0, EditPromotionValue);
        product.PromotionMinQty = Math.Max(0, EditPromotionMinQty);

        await db.SaveChangesAsync();

        product.Stock = await db.ProductStocks
            .Where(ps => ps.ProductId == product.Id)
            .SumAsync(ps => ps.Stock);

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "EDIT",
            Entity = "Product",
            EntityId = product.Id,
            BranchId = BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            Details = $"Edición producto '{product.Name}' en sucursal {BranchId}. Stock={EditStock}, Min={EditMinStock}."
        });

        await db.SaveChangesAsync();

        TempData["Flash"] = "Producto actualizado.";
        return RedirectToPage(new { q = Q, branchId = BranchId });
    }

    private async Task LoadAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString()))
            .ToList();

        if (Branches.Count == 0)
        {
            Items = [];
            Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
            return;
        }

        if (BranchId <= 0 || !Branches.Any(b => b.Value == BranchId.ToString()))
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();

        var query = db.ProductStocks
            .Include(x => x.Product)!.ThenInclude(p => p!.Category)
            .Where(x => x.BranchId == BranchId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            query = query.Where(x =>
                x.Product != null && (
                    x.Product.Name.Contains(Q) ||
                    x.Product.Barcode.Contains(Q) ||
                    x.Product.ProductNumber.ToString().Contains(Q)));
        }

        Items = await query
            .OrderBy(x => x.Product!.ProductNumber)
            .Select(x => new ProductBranchRow
            {
                ProductId = x.ProductId,
                ProductNumber = x.Product!.ProductNumber,
                Name = x.Product.Name,
                Barcode = x.Product.Barcode,
                CategoryId = x.Product.CategoryId,
                CategoryName = x.Product.Category != null ? x.Product.Category.Name : "General",
                Price = x.Product.Price,
                Cost = x.Product.Cost,
                PriceIncludesTax = x.Product.PriceIncludesTax,
                PromotionType = x.Product.PromotionType,
                PromotionValue = x.Product.PromotionValue,
                PromotionMinQty = x.Product.PromotionMinQty,
                Stock = x.Stock,
                MinStock = x.MinStock
            })
            .ToListAsync();
    }

    public class ProductBranchRow
    {
        public int ProductId { get; set; }
        public int ProductNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public bool PriceIncludesTax { get; set; }
        public string PromotionType { get; set; } = "None";
        public decimal PromotionValue { get; set; }
        public int PromotionMinQty { get; set; }
        public int Stock { get; set; }
        public int MinStock { get; set; }
    }
}
