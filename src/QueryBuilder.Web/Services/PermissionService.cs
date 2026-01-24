using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;

namespace QueryBuilder.Web.Services;

public class PermissionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;

    public PermissionService(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<bool> CanViewDashboardAsync(ClaimsPrincipal user, int dashboardId)
    {
        if (IsAdminOrEditor(user))
        {
            return true;
        }

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await HasShareAsync(userId, ShareEntityType.Dashboard, dashboardId);
    }

    public async Task<bool> CanViewQueryAsync(ClaimsPrincipal user, int queryId)
    {
        if (IsAdminOrEditor(user))
        {
            return true;
        }

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await HasShareAsync(userId, ShareEntityType.Query, queryId);
    }

    public async Task<List<int>> GetAccessibleDashboardIdsAsync(ClaimsPrincipal user)
    {
        if (IsAdminOrEditor(user))
        {
            return await _dbContext.Dashboards.AsNoTracking().Select(d => d.Id).ToListAsync();
        }

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<int>();
        }

        return await GetSharedEntityIdsAsync(userId, ShareEntityType.Dashboard);
    }

    public async Task<List<int>> GetAccessibleQueryIdsAsync(ClaimsPrincipal user)
    {
        if (IsAdminOrEditor(user))
        {
            return await _dbContext.Queries.AsNoTracking().Select(q => q.Id).ToListAsync();
        }

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<int>();
        }

        return await GetSharedEntityIdsAsync(userId, ShareEntityType.Query);
    }

    public async Task<bool> HasQueryEditAccessAsync(string userId, int queryId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await HasShareAsync(userId, ShareEntityType.Query, queryId, ShareAccessLevel.Edit);
    }

    private async Task<bool> HasShareAsync(string userId, ShareEntityType entityType, int entityId)
    {
        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync();

        return await _dbContext.Shares.AsNoTracking().AnyAsync(s =>
            s.EntityType == entityType &&
            s.EntityId == entityId &&
            ((s.UserId != null && s.UserId == userId) ||
             (s.GroupId != null && groupIds.Contains(s.GroupId.Value))));
    }

    private async Task<bool> HasShareAsync(string userId, ShareEntityType entityType, int entityId, ShareAccessLevel minimumAccessLevel)
    {
        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync();

        return await _dbContext.Shares.AsNoTracking().AnyAsync(s =>
            s.EntityType == entityType &&
            s.EntityId == entityId &&
            s.AccessLevel >= minimumAccessLevel &&
            ((s.UserId != null && s.UserId == userId) ||
             (s.GroupId != null && groupIds.Contains(s.GroupId.Value))));
    }

    private async Task<List<int>> GetSharedEntityIdsAsync(string userId, ShareEntityType entityType)
    {
        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync();

        return await _dbContext.Shares.AsNoTracking()
            .Where(s => s.EntityType == entityType &&
                        ((s.UserId != null && s.UserId == userId) ||
                         (s.GroupId != null && groupIds.Contains(s.GroupId.Value))))
            .Select(s => s.EntityId)
            .Distinct()
            .ToListAsync();
    }

    private static bool IsAdminOrEditor(ClaimsPrincipal user)
    {
        return user.IsInRole("Admin") || user.IsInRole("Editor");
    }
}
