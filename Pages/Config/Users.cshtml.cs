using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class UsersModel(AppDbContext db, IAlertEmailService emailService) : PageModel
{
    [BindProperty]
    public NewUserInput Input { get; set; } = new();

    public List<User> Users { get; private set; } = new();
    public List<Branch> Branches { get; private set; } = new();
    public List<SelectListItem> Profiles { get; private set; } = new();
    public List<SelectListItem> Tenants { get; private set; } = new();
    public Dictionary<int, HashSet<int>> UserBranchMap { get; private set; } = new();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await LoadAsync();
        var isSuperSession = IsSuperSession();
        var currentTenantId = await ResolveCurrentTenantIdAsync();

        if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrWhiteSpace(Input.Password) || string.IsNullOrWhiteSpace(Input.FullName))
        {
            TempData["Flash"] = "Completa nombre, usuario y contraseña.";
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(Input.Email))
        {
            Input.Email = Input.Email.Trim();
            if (!Input.Email.Contains('@'))
            {
                TempData["Flash"] = "Correo inválido.";
                return RedirectToPage();
            }
        }

        var usernameClean = Input.Username.Trim();
        var exists = await db.Users.AnyAsync(u => u.Username == usernameClean);
        if (exists)
        {
            TempData["Flash"] = "Ese usuario ya existe.";
            return RedirectToPage();
        }

        if (!isSuperSession)
        {
            Input.IsSuperUser = false;
            Input.TenantId = currentTenantId;
        }

        var profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == Input.ProfileId && p.IsActive);
        if (!Input.IsSuperUser && profile is null)
        {
            TempData["Flash"] = "Selecciona un perfil válido.";
            return RedirectToPage();
        }

        var tenantId = Input.IsSuperUser ? (int?)null : Input.TenantId;
        if (!Input.IsSuperUser && (!tenantId.HasValue || !await db.Tenants.AnyAsync(t => t.Id == tenantId.Value && t.IsActive)))
        {
            TempData["Flash"] = "Selecciona un tenant activo para el usuario.";
            return RedirectToPage();
        }

        var user = new User
        {
            Username = usernameClean,
            Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.Password.Trim()),
            FullName = Input.FullName.Trim(),
            Role = Input.IsSuperUser ? "SuperUser" : (profile!.IsAdmin ? "Admin" : profile.Name),
            PermissionProfileId = Input.IsSuperUser ? null : profile!.Id,
            TenantId = tenantId,
            IsSuperUser = Input.IsSuperUser,
            IsActive = true,
            ModulePermissions = Input.IsSuperUser ? string.Join(',', ModuleCatalog.All) : profile!.ModulePermissions
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (!Input.IsSuperUser)
        {
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
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var tenantCode = await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Code).FirstOrDefaultAsync() ?? "principal";
            var body =
                "Bienvenido a HN Nexus POS.\n\n" +
                $"Usuario: {user.Username}\n" +
                $"Contraseña temporal: {Input.Password.Trim()}\n" +
                $"Tenant: {tenantCode}\n\n" +
                "Por seguridad cambia tu contraseña al primer ingreso.";
            _ = await emailService.SendToAsync(user.Email, "Acceso HN Nexus POS", body, false);
        }

        TempData["Flash"] = "Usuario creado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(int id, string? fullName, string? email, string? password, int? profileId, bool isSuperUser, int? tenantId)
    {
        var isSuperSession = IsSuperSession();
        var currentTenantId = await ResolveCurrentTenantIdAsync();

        var user = await db.Users.Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            TempData["Flash"] = "Usuario no encontrado.";
            return RedirectToPage();
        }

        if (!isSuperSession && (user.IsSuperUser || user.TenantId != currentTenantId))
        {
            TempData["Flash"] = "No puedes editar usuarios de otro tenant.";
            return RedirectToPage();
        }

        if (!isSuperSession)
        {
            isSuperUser = false;
            tenantId = currentTenantId;
        }

        PermissionProfile? profile = null;
        if (!isSuperUser)
        {
            if (!profileId.HasValue)
            {
                TempData["Flash"] = "Perfil inválido.";
                return RedirectToPage();
            }

            profile = await db.PermissionProfiles.FirstOrDefaultAsync(p => p.Id == profileId.Value && p.IsActive);
            if (profile is null)
            {
                TempData["Flash"] = "Perfil inválido.";
                return RedirectToPage();
            }

            if (!tenantId.HasValue || !await db.Tenants.AnyAsync(t => t.Id == tenantId.Value && t.IsActive))
            {
                TempData["Flash"] = "Tenant inválido.";
                return RedirectToPage();
            }
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            user.FullName = fullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailClean = email.Trim();
            if (!emailClean.Contains('@'))
            {
                TempData["Flash"] = "Correo inválido.";
                return RedirectToPage();
            }
            user.Email = emailClean;
        }
        else
        {
            user.Email = null;
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.Trim());
        }

        user.IsSuperUser = isSuperUser;
        user.Role = isSuperUser ? "SuperUser" : (profile!.IsAdmin ? "Admin" : profile.Name);
        user.PermissionProfileId = isSuperUser ? null : profile!.Id;
        user.ModulePermissions = isSuperUser ? string.Join(',', ModuleCatalog.All) : profile!.ModulePermissions;
        user.TenantId = isSuperUser ? null : tenantId;

        var current = await db.UserBranchAccesses.Where(x => x.UserId == user.Id).ToListAsync();
        if (isSuperUser)
        {
            db.UserBranchAccesses.RemoveRange(current);
        }
        else
        {
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

            db.UserBranchAccesses.RemoveRange(current.Where(c => !selectedBranches.Contains(c.BranchId)));
            var currentSet = current.Select(c => c.BranchId).ToHashSet();
            foreach (var branchId in selectedBranches.Where(b => !currentSet.Contains(b)))
            {
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });
            }
        }

        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(user.Email))
        {
            _ = await emailService.SendToAsync(
                user.Email,
                "Cambio de contraseña HN Nexus POS",
                $"Tu contraseña fue actualizada.\nUsuario: {user.Username}\nSi no reconoces este cambio, contacta soporte.",
                false);
        }

        TempData["Flash"] = "Usuario actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var isSuperSession = IsSuperSession();
        var currentTenantId = await ResolveCurrentTenantIdAsync();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return RedirectToPage();
        }

        if (!isSuperSession && (user.IsSuperUser || user.TenantId != currentTenantId))
        {
            TempData["Flash"] = "No puedes desactivar usuarios de otro tenant.";
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
        var isSuperSession = IsSuperSession();
        var currentTenantId = await ResolveCurrentTenantIdAsync();

        var usersQuery = db.Users.Include(u => u.PermissionProfile).Include(u => u.Tenant).AsQueryable();
        if (!isSuperSession)
        {
            usersQuery = usersQuery.Where(u => !u.IsSuperUser && u.TenantId == currentTenantId);
        }

        Users = await usersQuery.OrderBy(x => x.FullName).ToListAsync();
        Branches = await db.Branches.Where(b => b.IsActive).OrderBy(x => x.Name).ToListAsync();
        Profiles = await db.PermissionProfiles
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();

        if (isSuperSession)
        {
            Tenants = await db.Tenants.Where(t => t.IsActive).OrderBy(t => t.Name)
                .Select(t => new SelectListItem($"{t.Code} - {t.Name}", t.Id.ToString()))
                .ToListAsync();
        }
        else
        {
            Tenants = await db.Tenants.Where(t => t.IsActive && t.Id == currentTenantId).OrderBy(t => t.Name)
                .Select(t => new SelectListItem($"{t.Code} - {t.Name}", t.Id.ToString()))
                .ToListAsync();
        }

        var accesses = await db.UserBranchAccesses.ToListAsync();
        UserBranchMap = accesses
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.BranchId).ToHashSet());
    }

    private bool IsSuperSession()
    {
        return User.IsInRole("SuperUser")
            || string.Equals(User.FindFirstValue("is_super"), "1", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int?> ResolveCurrentTenantIdAsync()
    {
        var tenantCode = User.FindFirstValue("tenant_code");
        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            var idFromCode = await db.Tenants
                .Where(t => t.IsActive && t.Code.ToLower() == tenantCode.ToLower())
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            if (idFromCode.HasValue)
            {
                return idFromCode;
            }
        }

        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdRaw, out var userId))
        {
            return await db.Users.Where(u => u.Id == userId).Select(u => u.TenantId).FirstOrDefaultAsync();
        }

        return null;
    }

    public class NewUserInput
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public int? ProfileId { get; set; }
        public int? TenantId { get; set; }
        public bool IsSuperUser { get; set; }
    }
}
