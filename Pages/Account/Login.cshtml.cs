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
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == Input.Username && x.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(Input.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña inválidos.");
            return Page();
        }

        var branches = await db.UserBranchAccesses
            .Where(x => x.UserId == user.Id)
            .Select(x => x.BranchId)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role),
            new("modules", user.ModulePermissions ?? string.Empty),
            new("branches", string.Join(',', branches))
        };

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

