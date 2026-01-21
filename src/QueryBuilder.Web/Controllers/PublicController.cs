using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Dashboards;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[AllowAnonymous]
public class PublicController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QueryRunner _queryRunner;
    private readonly PublicShareService _publicShareService;
    private readonly ILogger<PublicController> _logger;

    public PublicController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        PublicShareService publicShareService,
        ILogger<PublicController> logger)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _publicShareService = publicShareService;
        _logger = logger;
    }

    [HttpGet("public/d/{token}")]
    public async Task<IActionResult> Dashboard(string token)
    {
        var share = await _publicShareService.GetValidShareByTokenAsync(PublicShareEntityType.Dashboard, token);
        if (share == null)
        {
            return NotFound();
        }

        var model = await BuildDashboardViewModelAsync(share.EntityId);
        if (model == null)
        {
            return NotFound();
        }

        return View("Dashboard", model);
    }

    [HttpGet("public/embed/d/{token}")]
    public async Task<IActionResult> EmbedDashboard(string token)
    {
        var share = await _publicShareService.GetValidShareByTokenAsync(PublicShareEntityType.Dashboard, token);
        if (share == null)
        {
            return NotFound();
        }

        var model = await BuildDashboardViewModelAsync(share.EntityId);
        if (model == null)
        {
            return NotFound();
        }

        return View("DashboardEmbed", model);
    }

    [HttpGet("public/data/d/{token}/w/{widgetId}")]
    public async Task<IActionResult> WidgetData(string token, int widgetId)
    {
        var share = await _publicShareService.GetValidShareByTokenAsync(PublicShareEntityType.Dashboard, token);
        if (share == null)
        {
            return NotFound();
        }

        var widget = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Include(w => w.Visualization)
            .ThenInclude(v => v!.Query)
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.DashboardId == share.EntityId);

        if (widget?.Visualization?.Query == null)
        {
            return NotFound();
        }

        _logger.LogInformation("Auto-refresh public widget {WidgetId} for dashboard {DashboardId}", widgetId, share.EntityId);
        var result = await _queryRunner.RunAsync(widget.Visualization.Query.DataSourceId, widget.Visualization.Query.SqlText);
        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                errorMessage = "Refresh failed."
            });
        }

        return Ok(new
        {
            success = true,
            columns = result.Columns,
            rows = result.Rows
        });
    }

    private async Task<DashboardViewModel?> BuildDashboardViewModelAsync(int dashboardId)
    {
        var dashboard = await _dbContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dashboardId);
        if (dashboard == null)
        {
            return null;
        }

        var widgets = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.DashboardId == dashboardId)
            .Include(w => w.Visualization)
            .ThenInclude(v => v!.Query)
            .ToListAsync();

        var viewWidgets = new List<DashboardWidgetViewModel>();
        foreach (var widget in widgets)
        {
            if (widget.Visualization == null || widget.Visualization.Query == null)
            {
                continue;
            }

            var result = await _queryRunner.RunAsync(widget.Visualization.Query.DataSourceId, widget.Visualization.Query.SqlText);
            var config = JsonSerializer.Deserialize<VisualizationConfig>(widget.Visualization.ConfigJson) ?? new VisualizationConfig();

            viewWidgets.Add(new DashboardWidgetViewModel
            {
                Widget = widget,
                Visualization = widget.Visualization,
                Config = config,
                Result = result
            });
        }

        return new DashboardViewModel
        {
            Dashboard = dashboard,
            Widgets = viewWidgets
        };
    }
}
