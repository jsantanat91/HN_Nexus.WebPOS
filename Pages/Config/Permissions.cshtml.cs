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
    public List<Branch> Branches { get; private set; } = new();
    public List<PermissionProfile> Profiles { get; private set; } = new();

    [BindProperty]
    public string ProfileName { get; set; } = string.Empty;

    [BindProperty]
    public bool ProfileIsAdmin { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            TempData["Flash"] = "Nombre de perfil requerido.";
            return RedirectToPage();
        }

        var exists = await db.PermissionProfiles.AnyAsync(p => p.Name.ToLower() == ProfileName.Trim().ToLower() && p.IsActive);
        if (exists)
        {
            TempData["Flash"] = "Ese perfil ya existe.";
            return RedirectToPage();
        }

        var selectedModules = ModuleCatalog.All
            .Where(m => Request.Form[$"pmod_{m}"].Count > 0)
            .ToList();

        if (ProfileIsAdmin)
        {
            selectedModules = ModuleCatalog.All.ToList();
        }

        if (selectedModules.Count == 0)
        {
            TempData["Flash"] = "Selecciona al menos un módulo para el perfil.";
            return RedirectToPage();
        }

        db.PermissionProfiles.Add(new PermissionProfile
        {
            Name = ProfileName.Trim(),
            IsAdmin = ProfileIsAdmin,
            IsActive = true,
            ModulePermissions = string.Join(',', selectedModules)
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Perfil creado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync(int profileId, string profileName, bool profileIsAdmin)
    {
        var profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == profileId && p.IsActive);
        if (profile is null)
        {
            return RedirectToPage();
        }

        profile.Name = string.IsNullOrWhiteSpace(profileName) ? profile.Name : profileName.Trim();
        profile.IsAdmin = profileIsAdmin;

        var selectedModules = ModuleCatalog.All
            .Where(m => Request.Form[$"upmod_{m}"].Count > 0)
            .ToList();

        if (profile.IsAdmin)
        {
            selectedModules = ModuleCatalog.All.ToList();
        }

        profile.ModulePermissions = string.Join(',', selectedModules);
        await db.SaveChangesAsync();

        // actualiza usuarios que usan este perfil
        var users = await db.Users.Where(u => u.PermissionProfileId == profile.Id).ToListAsync();
        foreach (var u in users)
        {
            u.Role = profile.IsAdmin ? "Admin" : profile.Name;
            u.ModulePermissions = profile.ModulePermissions;
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Perfil actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateProfileAsync(int profileId)
    {
        var profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == profileId && p.IsActive);
        if (profile is null)
        {
            return RedirectToPage();
        }

        var inUse = await db.Users.AnyAsync(u => u.PermissionProfileId == profileId && u.IsActive);
        if (inUse)
        {
            TempData["Flash"] = "No se puede desactivar el perfil porque está asignado a usuarios activos.";
            return RedirectToPage();
        }

        profile.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Perfil desactivado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return RedirectToPage();
        }

        var selectedModules = ModuleCatalog.All
            .Where(m => Request.Form[$"perm_{m}"].Count > 0)
            .ToList();

        if (user.Role == "Admin")
        {
            user.ModulePermissions = string.Join(',', ModuleCatalog.All);
        }
        else
        {
            user.ModulePermissions = string.Join(',', selectedModules);
        }

        var selectedBranches = await db.Branches
            .Where(b => b.IsActive)
            .Select(b => b.Id)
            .Where(id => Request.Form[$"branch_{id}"].Count > 0)
            .ToListAsync();

        if (selectedBranches.Count == 0)
        {
            var defaultBranch = await db.Branches.Where(b => b.IsActive).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (defaultBranch is not null)
            {
                selectedBranches.Add(defaultBranch.Id);
            }
        }

        var current = await db.UserBranchAccesses.Where(x => x.UserId == userId).ToListAsync();
        db.UserBranchAccesses.RemoveRange(current.Where(c => !selectedBranches.Contains(c.BranchId)));

        var currentBranchIds = current.Select(c => c.BranchId).ToHashSet();
        foreach (var branchId in selectedBranches.Where(id => !currentBranchIds.Contains(id)))
        {
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = userId, BranchId = branchId });
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = $"Permisos y sucursales actualizados para {user.FullName}.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Branches = await db.Branches.Where(b => b.IsActive).OrderBy(x => x.Name).ToListAsync();
        Profiles = await db.PermissionProfiles.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

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

        var access = await db.UserBranchAccesses.ToListAsync();
        foreach (var user in Users)
        {
            user.BranchSet = access.Where(a => a.UserId == user.Id).Select(a => a.BranchId).ToHashSet();
        }
    }

    public class UserPermissionRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Permissions { get; set; } = string.Empty;
        public HashSet<int> BranchSet { get; set; } = [];

        public HashSet<string> PermissionSet => Permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();
    }
}
