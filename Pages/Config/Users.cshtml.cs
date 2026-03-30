using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class UsersModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public NewUserInput Input { get; set; } = new();

    public List<User> Users { get; private set; } = new();
    public List<Branch> Branches { get; private set; } = new();

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

        var exists = await db.Users.AnyAsync(u => u.Username == Input.Username);
        if (exists)
        {
            TempData["Flash"] = "Ese usuario ya existe.";
            return RedirectToPage();
        }

        var selectedModules = ModuleCatalog.All
            .Where(m => Request.Form[$"mod_{m}"].Count > 0)
            .ToList();

        if (Input.Role == "Admin")
        {
            selectedModules = ModuleCatalog.All.ToList();
        }

        var user = new User
        {
            Username = Input.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.Password.Trim()),
            FullName = Input.FullName.Trim(),
            Role = Input.Role,
            IsActive = true,
            ModulePermissions = string.Join(',', selectedModules)
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
        TempData["Flash"] = "Usuario creado con permisos y sucursales asignadas.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return RedirectToPage();
        }

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();
        TempData["Flash"] = "Estatus de usuario actualizado.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Users = await db.Users.OrderBy(x => x.FullName).ToListAsync();
        Branches = await db.Branches.Where(b => b.IsActive).OrderBy(x => x.Name).ToListAsync();
    }

    public class NewUserInput
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Cajero";
    }
}
