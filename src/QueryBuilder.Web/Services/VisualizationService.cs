using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class VisualizationService : IVisualizationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PermissionService _permissionService;
    private readonly UserManager<IdentityUser> _userManager;

    public VisualizationService(ApplicationDbContext dbContext, PermissionService permissionService, UserManager<IdentityUser> userManager)
    {
        _dbContext = dbContext;
        _permissionService = permissionService;
        _userManager = userManager;
    }

    public async Task<VisualizationDeleteResult> DeleteVisualizationAsync(int visualizationId, ClaimsPrincipal user)
    {
        var result = new VisualizationDeleteResult
        {
            Success = false
        };

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            result.IsForbidden = true;
            result.Message = "You do not have permission to delete this visualization.";
            return result;
        }

        var isAdmin = user.IsInRole("Admin");
        var isEditor = user.IsInRole("Editor");
        if (!isAdmin && !isEditor)
        {
            result.IsForbidden = true;
            result.Message = "You do not have permission to delete this visualization.";
            return result;
        }

        var visualization = await _dbContext.Visualizations
            .IgnoreQueryFilters()
            .Include(v => v.Query)
            .FirstOrDefaultAsync(v => v.Id == visualizationId);

        if (visualization == null || visualization.IsDeleted)
        {
            result.Message = "Visualization not found.";
            return result;
        }

        if (visualization.Query == null)
        {
            result.Message = "Visualization not found.";
            return result;
        }

        if (!isAdmin)
        {
            var isOwner = visualization.Query.CreatedById == userId;
            var hasEditShare = await _permissionService.HasQueryEditAccessAsync(userId, visualization.QueryId);
            if (!isOwner && !hasEditShare)
            {
                result.IsForbidden = true;
                result.Message = "You do not have permission to delete this visualization.";
                return result;
            }
        }

        var dashboardIds = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.VisualizationId == visualizationId)
            .Select(w => w.DashboardId)
            .Distinct()
            .ToListAsync();

        if (dashboardIds.Count > 0)
        {
            var dashboardNames = await _dbContext.Dashboards
                .AsNoTracking()
                .Where(d => dashboardIds.Contains(d.Id))
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToListAsync();

            result.Message = "Cannot delete visualization because it is used in dashboards.";
            result.AffectedDashboards = dashboardNames;
            result.QueryId = visualization.QueryId;
            return result;
        }

        visualization.IsDeleted = true;
        visualization.DeletedAt = DateTimeOffset.UtcNow;
        visualization.DeletedByUserId = userId;
        visualization.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        result.Success = true;
        result.Message = "Visualization deleted.";
        result.QueryId = visualization.QueryId;
        return result;
    }
}
