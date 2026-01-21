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

    public PublicController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        PublicShareService publicShareService)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _publicShareService = publicShareService;
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
