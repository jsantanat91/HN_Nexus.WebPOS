using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Services;

public interface IUserContextService
{
    int? GetUserId(ClaimsPrincipal user);
    Task<List<Branch>> GetAccessibleBranchesAsync(ClaimsPrincipal user);
}

public class UserContextService(AppDbContext db) : IUserContextService
{
    public int? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    public async Task<List<Branch>> GetAccessibleBranchesAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            return await db.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return [];
        }

        return await db.UserBranchAccesses
            .Where(x => x.UserId == userId && x.Branch != null && x.Branch.IsActive)
            .Select(x => x.Branch!)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
