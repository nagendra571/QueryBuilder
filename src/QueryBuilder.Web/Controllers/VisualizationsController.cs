using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class VisualizationsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QueryRunner _queryRunner;

    public VisualizationsController(ApplicationDbContext dbContext, QueryRunner queryRunner)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
    }

    public async Task<IActionResult> Create(int queryId)
    {
        var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queryId);
        if (query == null)
        {
            return NotFound();
        }

        var result = await _queryRunner.RunAsync(query.DataSourceId, query.SqlText);
        var model = new VisualizationEditViewModel
        {
            QueryId = query.Id,
            Name = $"{query.Name} Chart",
            Columns = result.Columns
        };

        ViewData["VisualizationTypes"] = new SelectList(Enum.GetValues<VisualizationType>());
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VisualizationEditViewModel model)
    {
        var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == model.QueryId);
        if (query == null)
        {
            return NotFound();
        }

        var result = await _queryRunner.RunAsync(query.DataSourceId, query.SqlText);
        model.Columns = result.Columns;
        model.Result = string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase) ? result : null;

        ViewData["VisualizationTypes"] = new SelectList(Enum.GetValues<VisualizationType>());

        if (string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var config = new VisualizationConfig
        {
            XColumn = model.XColumn,
            YColumn = model.YColumn,
            LabelColumn = model.LabelColumn,
            ValueColumn = model.ValueColumn
        };

        var visualization = new Visualization
        {
            QueryId = model.QueryId,
            Name = model.Name.Trim(),
            Type = model.Type,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Visualizations.Add(visualization);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = visualization.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var visualization = await _dbContext.Visualizations
            .Include(v => v.Query)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (visualization == null || visualization.Query == null)
        {
            return NotFound();
        }

        var result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, visualization.Query.SqlText);
        var config = JsonSerializer.Deserialize<VisualizationConfig>(visualization.ConfigJson) ?? new VisualizationConfig();

        var model = new VisualizationDetailsViewModel
        {
            Visualization = visualization,
            Result = result,
            Config = config,
            QueryName = visualization.Query.Name
        };

        return View(model);
    }
}
