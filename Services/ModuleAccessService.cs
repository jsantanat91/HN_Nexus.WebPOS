using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Services;

public interface IModuleAccessService
{
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string moduleCode);
}

public class ModuleAccessService(AppDbContext db) : IModuleAccessService
{
    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string moduleCode)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        var userIdRaw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdRaw, out var userId))
        {
            return false;
        }

        var permissions = await db.Users
            .Where(x => x.Id == userId)
            .Select(x => x.ModulePermissions)
            .FirstOrDefaultAsync() ?? string.Empty;

        var set = permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();

        return set.Contains(moduleCode.ToLowerInvariant());
    }
}

