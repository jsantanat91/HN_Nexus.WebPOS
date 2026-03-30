using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Expenses;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Expense> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await db.Expenses.OrderByDescending(x => x.Date).Take(200).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string description, decimal amount, string category)
    {
        if (string.IsNullOrWhiteSpace(description) || amount <= 0)
        {
            TempData["Flash"] = "Captura descripcion y monto valido.";
            return RedirectToPage();
        }

        db.Expenses.Add(new Expense
        {
            Description = description,
            Amount = amount,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
            Date = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Gasto registrado.";
        return RedirectToPage();
    }
}
