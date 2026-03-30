using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class PermissionsModel(AppDbContext db) : PageModel
{
    public List<UserPermissionRow> Users { get; private set; } = new();
    public Dictionary<string, string> Modules { get; } = ModuleCatalog.Labels;

    public async Task OnGetAsync()
    {
        Users = await db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserPermissionRow
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role,
                IsActive = u.IsActive,
                Permissions = u.ModulePermissions
            })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync(int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return RedirectToPage();
        }

        if (user.Role == "Admin")
        {
            user.ModulePermissions = string.Join(',', ModuleCatalog.All);
            await db.SaveChangesAsync();
            TempData["Flash"] = "Usuario administrador conserva acceso completo.";
            return RedirectToPage();
        }

        var selected = ModuleCatalog.All
            .Where(m => Request.Form[$"perm_{m}"].Count > 0)
            .ToList();

        user.ModulePermissions = string.Join(',', selected);
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Permisos actualizados para {user.FullName}.";
        return RedirectToPage();
    }

    public class UserPermissionRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Permissions { get; set; } = string.Empty;

        public HashSet<string> PermissionSet => Permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();
    }
}
