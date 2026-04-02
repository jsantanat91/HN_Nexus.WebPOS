using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "SuperOnly")]
public class TenantsModel(AppDbContext db) : PageModel
{
    public List<Tenant> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await db.Tenants.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, string code, string? host)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Flash"] = "Captura nombre y código del tenant.";
            return RedirectToPage();
        }

        var cleanCode = NormalizeCode(code);
        var schema = $"hn_{cleanCode}";

        if (await db.Tenants.AnyAsync(x => x.Code == cleanCode || x.SchemaName == schema))
        {
            TempData["Flash"] = "Ya existe un tenant con ese código/schema.";
            return RedirectToPage();
        }

        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\";");

        var tenantTables = new[]
        {
            "AppConfigs", "AuditLogs", "AccountingClosures", "Branches", "CashCuts", "CashShifts", "Categories", "CfdiDocuments", "CfdiVaultFiles",
            "Customers", "Expenses", "ProductIngredients", "ProductLots", "Products", "ProductStocks", "PromotionRules", "ReplenishmentRules",
            "SaleDetails", "Sales", "SaleReturns", "SaleReturnLines", "StockTransfers", "SupplierOrders", "Suppliers", "Warehouses", "WarehouseStocks",
            "PriceLists", "PriceListItems", "CycleCounts", "CycleCountLines", "LotTraces", "AppTelemetryEvents", "UserBranchAccesses"
        };

        foreach (var table in tenantTables)
        {
            await db.Database.ExecuteSqlRawAsync($"CREATE TABLE IF NOT EXISTS \"{schema}\".\"{table}\" (LIKE public.\"{table}\" INCLUDING ALL);");
        }

        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"{schema}\".\"Categories\" (\"Name\") SELECT 'General' WHERE NOT EXISTS (SELECT 1 FROM \"{schema}\".\"Categories\");");
        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"{schema}\".\"Branches\" (\"Code\",\"Name\",\"Address\",\"IsActive\") SELECT 'MATRIZ','Sucursal Matriz','Pendiente por configurar',true WHERE NOT EXISTS (SELECT 1 FROM \"{schema}\".\"Branches\");");
        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"{schema}\".\"Suppliers\" (\"Name\",\"ContactName\",\"Phone\",\"Email\",\"IsActive\") SELECT 'Proveedor General','','','',true WHERE NOT EXISTS (SELECT 1 FROM \"{schema}\".\"Suppliers\");");
        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"{schema}\".\"PriceLists\" (\"Name\",\"IsActive\",\"IsWholesale\") SELECT 'Mostrador',true,false WHERE NOT EXISTS (SELECT 1 FROM \"{schema}\".\"PriceLists\");");
        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"{schema}\".\"Warehouses\" (\"BranchId\",\"Code\",\"Name\",\"IsActive\") SELECT 1,'PRINCIPAL','Almacén Principal',true WHERE NOT EXISTS (SELECT 1 FROM \"{schema}\".\"Warehouses\");");

        db.Tenants.Add(new Tenant
        {
            Name = name.Trim(),
            Code = cleanCode,
            SchemaName = schema,
            Host = string.IsNullOrWhiteSpace(host) ? null : host.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Tenant creado. Schema: {schema}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var item = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return RedirectToPage();
        }

        if (item.SchemaName == "public")
        {
            TempData["Flash"] = "No se puede desactivar el tenant principal.";
            return RedirectToPage();
        }

        item.IsActive = !item.IsActive;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Estatus de tenant actualizado.";
        return RedirectToPage();
    }

    private static string NormalizeCode(string code)
    {
        var raw = (code ?? string.Empty).Trim().ToLowerInvariant();
        var chars = raw.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray();
        var outCode = new string(chars);
        if (string.IsNullOrWhiteSpace(outCode)) outCode = "cliente";
        if (char.IsDigit(outCode[0])) outCode = "t" + outCode;
        return outCode;
    }
}
