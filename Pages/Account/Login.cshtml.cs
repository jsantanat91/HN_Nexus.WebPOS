using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Account;

public class LoginModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? TenantCode { get; set; }
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var username = (Input.Username ?? string.Empty).Trim();
        var tenantCode = (Input.TenantCode ?? string.Empty).Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Username == username && x.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(Input.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña inválidos.");
            return Page();
        }

        var isSuper = user.IsSuperUser || string.Equals(user.Role, "SuperUser", StringComparison.OrdinalIgnoreCase);

        if (!isSuper)
        {
            string? resolvedCode = null;
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                resolvedCode = tenantCode;
            }
            else
            {
                var host = (Request.Host.Host ?? string.Empty).ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(host))
                {
                    resolvedCode = await db.Tenants
                        .Where(t => t.IsActive && t.Host != null && t.Host.ToLower() == host)
                        .Select(t => t.Code.ToLower())
                        .FirstOrDefaultAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedCode))
            {
                ModelState.AddModelError(string.Empty, "Captura el código de tenant para acceder.");
                return Page();
            }

            if (user.Tenant is null || !user.Tenant.IsActive || !string.Equals(user.Tenant.Code, resolvedCode, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "El usuario no pertenece a ese tenant.");
                return Page();
            }
        }

        var branches = await db.UserBranchAccesses
            .Where(x => x.UserId == user.Id)
            .Select(x => x.BranchId)
            .ToListAsync();

        var tenantSchema = user.Tenant?.SchemaName ?? "public";
        var tenantCodeClaim = user.Tenant?.Code ?? "principal";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role),
            new("modules", user.ModulePermissions ?? string.Empty),
            new("branches", string.Join(',', branches)),
            new("tenant_schema", tenantSchema),
            new("tenant_code", tenantCodeClaim),
            new("is_super", isSuper ? "1" : "0")
        };

        if (isSuper && !string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "SuperUser"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
