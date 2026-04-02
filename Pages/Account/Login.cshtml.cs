using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Account;

public class LoginModel(AppDbContext db, IAlertEmailService emailService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public RecoverInput Recovery { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? TenantCode { get; set; }
    }

    public class RecoverInput
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string? TenantCode { get; set; }
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ModelState.Remove($"{nameof(Recovery)}.{nameof(Recovery.UsernameOrEmail)}");
        ModelState.Remove($"{nameof(Recovery)}.{nameof(Recovery.TenantCode)}");

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
                var normalized = NormalizeTenantCode(tenantCode);
                resolvedCode = await db.Tenants
                    .Where(t => t.IsActive && (t.Code.ToLower() == tenantCode || t.Code.ToLower() == normalized))
                    .Select(t => t.Code.ToLower())
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(resolvedCode))
                {
                    resolvedCode = normalized;
                }
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

    public async Task<IActionResult> OnPostRecoverPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Recovery.UsernameOrEmail))
        {
            TempData["Flash"] = "Captura usuario o correo para recuperar contraseña.";
            return RedirectToPage();
        }

        var input = Recovery.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Tenant)
            .Where(u => u.IsActive && (u.Username.ToLower() == input || (u.Email != null && u.Email.ToLower() == input)))
            .FirstOrDefaultAsync();

        if (user is null)
        {
            TempData["Flash"] = "No se encontró un usuario activo con ese dato.";
            return RedirectToPage();
        }

        var isSuper = user.IsSuperUser || string.Equals(user.Role, "SuperUser", StringComparison.OrdinalIgnoreCase);
        if (!isSuper)
        {
            var tenantCode = (Recovery.TenantCode ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                TempData["Flash"] = "Para recuperar contraseña, indica el tenant.";
                return RedirectToPage();
            }

            var normalized = NormalizeTenantCode(tenantCode);
            var code = user.Tenant?.Code?.ToLowerInvariant() ?? string.Empty;
            if (code != tenantCode && code != normalized)
            {
                TempData["Flash"] = "El usuario no pertenece al tenant indicado.";
                return RedirectToPage();
            }
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            TempData["Flash"] = "El usuario no tiene correo configurado. Pide al administrador que lo capture.";
            return RedirectToPage();
        }

        var newPassword = GenerateTemporaryPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();

        var tenantText = user.Tenant?.Code ?? "principal";
        var body =
            "Recuperación de contraseña HN Nexus POS.\n\n" +
            $"Usuario: {user.Username}\n" +
            $"Nueva contraseña temporal: {newPassword}\n" +
            $"Tenant: {tenantText}\n\n" +
            "Te recomendamos cambiarla inmediatamente después de iniciar sesión.";

        var sent = await emailService.SendToAsync(user.Email, "Recuperación de acceso HN Nexus POS", body, false);
        TempData["Flash"] = sent
            ? "Recuperación enviada por correo."
            : "No fue posible enviar el correo. Revisa SMTP en Configuración General.";
        return RedirectToPage();
    }

    private static string NormalizeTenantCode(string value)
    {
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant();
        var chars = raw.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray();
        var outCode = new string(chars);
        if (string.IsNullOrWhiteSpace(outCode))
        {
            return "cliente";
        }

        if (char.IsDigit(outCode[0]))
        {
            outCode = "t" + outCode;
        }

        return outCode;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$";
        var bytes = new byte[10];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var output = new char[10];
        for (var i = 0; i < bytes.Length; i++)
        {
            output[i] = chars[bytes[i] % chars.Length];
        }
        return new string(output);
    }
}
