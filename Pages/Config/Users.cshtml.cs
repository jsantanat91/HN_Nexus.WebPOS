using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class UsersModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public NewUserInput Input { get; set; } = new();

    public List<User> Users { get; private set; } = new();
    public List<Branch> Branches { get; private set; } = new();
    public List<SelectListItem> Profiles { get; private set; } = new();
    public Dictionary<int, HashSet<int>> UserBranchMap { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await LoadAsync();

        if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrWhiteSpace(Input.Password) || string.IsNullOrWhiteSpace(Input.FullName))
        {
            TempData["Flash"] = "Completa nombre, usuario y contraseña.";
            return RedirectToPage();
        }

        var exists = await db.Users.AnyAsync(u => u.Username == Input.Username.Trim());
        if (exists)
        {
            TempData["Flash"] = "Ese usuario ya existe.";
            return RedirectToPage();
        }

        var profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == Input.ProfileId && p.IsActive);
        if (profile is null)
        {
            TempData["Flash"] = "Selecciona un perfil válido.";
            return RedirectToPage();
        }

        var user = new User
        {
            Username = Input.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.Password.Trim()),
            FullName = Input.FullName.Trim(),
            Role = profile.IsAdmin ? "Admin" : profile.Name,
            PermissionProfileId = profile.Id,
            IsActive = true,
            ModulePermissions = profile.ModulePermissions
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var selectedBranches = Branches
            .Where(b => Request.Form[$"bra_{b.Id}"].Count > 0)
            .Select(b => b.Id)
            .ToList();

        if (selectedBranches.Count == 0 && Branches.Count > 0)
        {
            selectedBranches.Add(Branches[0].Id);
        }

        foreach (var branchId in selectedBranches)
        {
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Usuario creado con perfil y sucursales asignadas.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(int id, string fullName, string? password, int profileId)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return RedirectToPage();
        }

        var profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == profileId && p.IsActive);
        if (profile is null)
        {
            TempData["Flash"] = "Perfil inválido.";
            return RedirectToPage();
        }

        user.FullName = string.IsNullOrWhiteSpace(fullName) ? user.FullName : fullName.Trim();
        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.Trim());
        }

        user.PermissionProfileId = profile.Id;
        user.Role = profile.IsAdmin ? "Admin" : profile.Name;
        user.ModulePermissions = profile.ModulePermissions;

        var selectedBranches = await db.Branches
            .Where(b => b.IsActive)
            .Select(b => b.Id)
            .Where(branchId => Request.Form[$"ebr_{branchId}"].Count > 0)
            .ToListAsync();

        if (selectedBranches.Count == 0)
        {
            var first = await db.Branches.Where(b => b.IsActive).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (first is not null)
            {
                selectedBranches.Add(first.Id);
            }
        }

        var current = await db.UserBranchAccesses.Where(x => x.UserId == user.Id).ToListAsync();
        db.UserBranchAccesses.RemoveRange(current.Where(c => !selectedBranches.Contains(c.BranchId)));

        var currentSet = current.Select(c => c.BranchId).ToHashSet();
        foreach (var branchId in selectedBranches.Where(b => !currentSet.Contains(b)))
        {
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });
        }

        await db.SaveChangesAsync();
        TempData["Flash"] = "Usuario actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return RedirectToPage();
        }

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash"] = "No se puede desactivar el usuario administrador principal.";
            return RedirectToPage();
        }

        user.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Usuario desactivado.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Users = await db.Users.Include(u => u.PermissionProfile).OrderBy(x => x.FullName).ToListAsync();
        Branches = await db.Branches.Where(b => b.IsActive).OrderBy(x => x.Name).ToListAsync();
        Profiles = await db.PermissionProfiles
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();

        var accesses = await db.UserBranchAccesses.ToListAsync();
        UserBranchMap = accesses
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.BranchId).ToHashSet());
    }

    public class NewUserInput
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int ProfileId { get; set; }
    }
}
