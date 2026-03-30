using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class SuppliersModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Supplier Input { get; set; } = new();

    public List<Supplier> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            TempData["Flash"] = "Nombre de proveedor requerido.";
            return RedirectToPage();
        }

        var supplier = new Supplier
        {
            Name = Input.Name.Trim(),
            ContactName = Input.ContactName.Trim(),
            Phone = Input.Phone.Trim(),
            Email = Input.Email.Trim(),
            IsActive = true
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        TempData["Flash"] = "Proveedor agregado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(int id, string name, string contactName, string phone, string email)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return RedirectToPage();
        }

        supplier.Name = string.IsNullOrWhiteSpace(name) ? supplier.Name : name.Trim();
        supplier.ContactName = contactName?.Trim() ?? string.Empty;
        supplier.Phone = phone?.Trim() ?? string.Empty;
        supplier.Email = email?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        TempData["Flash"] = "Proveedor actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return RedirectToPage();
        }

        supplier.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Proveedor desactivado.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await db.Suppliers.OrderBy(s => s.Name).ToListAsync();
    }
}
