using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class VisualizationsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QueryRunner _queryRunner;
    private readonly ILogger<VisualizationsController> _logger;
    private readonly IQueryParameterService _parameterService;
    private static readonly HashSet<int> AllowedRefreshIntervals = new()
    {
        30, 60, 300, 600, 1800, 3600
    };

    public VisualizationsController(ApplicationDbContext dbContext, QueryRunner queryRunner, ILogger<VisualizationsController> logger, IQueryParameterService parameterService)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _logger = logger;
        _parameterService = parameterService;
    }

    public async Task<IActionResult> Create(int queryId)
    {
        var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queryId);
        if (query == null)
        {
            return NotFound();
        }

        var definitions = await BuildParameterDefinitionsAsync(query.Id);
        var parameterValues = await GetLatestParameterValuesAsync(query.Id);
        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = query.SqlText,
            Definitions = definitions.Select(d => new QueryParameterDefinition
            {
                Id = d.Id,
                Name = d.Name,
                Title = d.Title,
                Type = d.Type,
                DefaultValue = d.DefaultValue,
                IsRequired = d.IsRequired,
                SettingsJson = d.SettingsJson
            }).ToList(),
            Values = parameterValues,
            AllowText = true
        });

        QueryResultViewModel result;
        if (!applyResult.Success)
        {
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(query.DataSourceId, applyResult.Sql);
        }
        var model = new VisualizationEditViewModel
        {
            QueryId = query.Id,
            Name = $"{query.Name} Chart",
            Columns = result.Columns,
            ShowLegend = true,
            IsAutoRefreshEnabled = false,
            AutoRefreshIntervalSeconds = null,
            UseHorizontalBars = false,
            UseFloatingBars = false,
            UseStackedBars = false,
            LineInterpolationMode = "default",
            ParameterDefinitions = definitions,
            ParameterValues = parameterValues
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

        var definitions = await BuildParameterDefinitionsAsync(query.Id);
        model.ParameterDefinitions = definitions;

        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = query.SqlText,
            Definitions = definitions.Select(d => new QueryParameterDefinition
            {
                Id = d.Id,
                Name = d.Name,
                Title = d.Title,
                Type = d.Type,
                DefaultValue = d.DefaultValue,
                IsRequired = d.IsRequired,
                SettingsJson = d.SettingsJson
            }).ToList(),
            Values = model.ParameterValues,
            AllowText = true
        });

        QueryResultViewModel result;
        if (!applyResult.Success)
        {
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(query.DataSourceId, applyResult.Sql);
        }
        model.Columns = result.Columns;
        model.Result = string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase) ? result : null;
        if (model.YColumns.Count == 0 && !string.IsNullOrWhiteSpace(model.YColumn))
        {
            model.YColumns.Add(model.YColumn);
        }

        ViewData["VisualizationTypes"] = new SelectList(Enum.GetValues<VisualizationType>());

        if (string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        if (model.IsAutoRefreshEnabled && model.AutoRefreshIntervalSeconds == null)
        {
            ModelState.AddModelError(nameof(model.AutoRefreshIntervalSeconds), "Refresh interval is required.");
        }
        else if (model.IsAutoRefreshEnabled && !AllowedRefreshIntervals.Contains(model.AutoRefreshIntervalSeconds.Value))
        {
            ModelState.AddModelError(nameof(model.AutoRefreshIntervalSeconds), "Refresh interval is not supported.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var config = new VisualizationConfig
        {
            XColumn = model.XColumn,
            YColumn = model.YColumn,
            YColumns = model.YColumns,
            UseHorizontalBars = model.UseHorizontalBars || model.Type == VisualizationType.HorizontalBar,
            UseStackedBars = model.UseStackedBars,
            UseFloatingBars = model.UseFloatingBars || model.Type == VisualizationType.FloatingBar,
            RangeStartColumn = model.RangeStartColumn,
            RangeEndColumn = model.RangeEndColumn,
            LabelColumn = model.LabelColumn,
            ValueColumn = model.ValueColumn,
            GroupByColumn = model.GroupByColumn,
            ShowLegend = model.ShowLegend,
            LineInterpolationMode = model.LineInterpolationMode
        };

        var visualization = new Visualization
        {
            QueryId = model.QueryId,
            Name = model.Name.Trim(),
            Type = model.Type,
            ConfigJson = JsonSerializer.Serialize(config),
            IsAutoRefreshEnabled = model.IsAutoRefreshEnabled,
            AutoRefreshIntervalSeconds = model.IsAutoRefreshEnabled ? model.AutoRefreshIntervalSeconds : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Visualizations.Add(visualization);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = visualization.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var visualization = await _dbContext.Visualizations
            .Include(v => v.Query)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (visualization == null || visualization.Query == null)
        {
            return NotFound();
        }

        var definitions = await BuildParameterDefinitionsAsync(visualization.QueryId);
        var parameterValues = await GetLatestParameterValuesAsync(visualization.QueryId);
        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = visualization.Query.SqlText,
            Definitions = definitions.Select(d => new QueryParameterDefinition
            {
                Id = d.Id,
                Name = d.Name,
                Title = d.Title,
                Type = d.Type,
                DefaultValue = d.DefaultValue,
                IsRequired = d.IsRequired,
                SettingsJson = d.SettingsJson
            }).ToList(),
            Values = parameterValues,
            AllowText = true
        });

        QueryResultViewModel result;
        if (!applyResult.Success)
        {
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        }
        var config = JsonSerializer.Deserialize<VisualizationConfig>(visualization.ConfigJson) ?? new VisualizationConfig();
        var model = new VisualizationEditViewModel
        {
            Id = visualization.Id,
            QueryId = visualization.QueryId,
            Name = visualization.Name,
            Type = visualization.Type,
            XColumn = config.XColumn,
            YColumn = config.YColumn,
            YColumns = config.YColumns.Count > 0 ? config.YColumns : (string.IsNullOrWhiteSpace(config.YColumn) ? new List<string>() : new List<string> { config.YColumn }),
            UseHorizontalBars = config.UseHorizontalBars || visualization.Type == VisualizationType.HorizontalBar,
            UseStackedBars = config.UseStackedBars,
            UseFloatingBars = config.UseFloatingBars || visualization.Type == VisualizationType.FloatingBar,
            RangeStartColumn = config.RangeStartColumn,
            RangeEndColumn = config.RangeEndColumn,
            LabelColumn = config.LabelColumn,
            ValueColumn = config.ValueColumn,
            GroupByColumn = config.GroupByColumn,
            ShowLegend = config.ShowLegend,
            LineInterpolationMode = string.IsNullOrWhiteSpace(config.LineInterpolationMode) ? "default" : config.LineInterpolationMode,
            Columns = result.Columns,
            IsAutoRefreshEnabled = visualization.IsAutoRefreshEnabled,
            AutoRefreshIntervalSeconds = visualization.AutoRefreshIntervalSeconds,
            ParameterDefinitions = definitions,
            ParameterValues = parameterValues
        };

        ViewData["VisualizationTypes"] = new SelectList(Enum.GetValues<VisualizationType>());
        ViewData["Title"] = "Edit Visualization";
        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VisualizationEditViewModel model)
    {
        var visualization = await _dbContext.Visualizations.FirstOrDefaultAsync(v => v.Id == id);
        if (visualization == null)
        {
            return NotFound();
        }

        var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == visualization.QueryId);
        if (query == null)
        {
            return NotFound();
        }

        var definitions = await BuildParameterDefinitionsAsync(query.Id);
        model.ParameterDefinitions = definitions;

        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = query.SqlText,
            Definitions = definitions.Select(d => new QueryParameterDefinition
            {
                Id = d.Id,
                Name = d.Name,
                Title = d.Title,
                Type = d.Type,
                DefaultValue = d.DefaultValue,
                IsRequired = d.IsRequired,
                SettingsJson = d.SettingsJson
            }).ToList(),
            Values = model.ParameterValues,
            AllowText = true
        });

        QueryResultViewModel result;
        if (!applyResult.Success)
        {
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(query.DataSourceId, applyResult.Sql);
        }
        model.Columns = result.Columns;
        model.Result = string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase) ? result : null;
        if (model.YColumns.Count == 0 && !string.IsNullOrWhiteSpace(model.YColumn))
        {
            model.YColumns.Add(model.YColumn);
        }

        ViewData["VisualizationTypes"] = new SelectList(Enum.GetValues<VisualizationType>());
        ViewData["Title"] = "Edit Visualization";

        if (string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase))
        {
            return View("Create", model);
        }

        if (model.IsAutoRefreshEnabled && model.AutoRefreshIntervalSeconds == null)
        {
            ModelState.AddModelError(nameof(model.AutoRefreshIntervalSeconds), "Refresh interval is required.");
        }
        else if (model.IsAutoRefreshEnabled && !AllowedRefreshIntervals.Contains(model.AutoRefreshIntervalSeconds.Value))
        {
            ModelState.AddModelError(nameof(model.AutoRefreshIntervalSeconds), "Refresh interval is not supported.");
        }

        if (!ModelState.IsValid)
        {
            return View("Create", model);
        }

        var config = new VisualizationConfig
        {
            XColumn = model.XColumn,
            YColumn = model.YColumn,
            YColumns = model.YColumns,
            UseHorizontalBars = model.UseHorizontalBars || model.Type == VisualizationType.HorizontalBar,
            UseStackedBars = model.UseStackedBars,
            UseFloatingBars = model.UseFloatingBars || model.Type == VisualizationType.FloatingBar,
            RangeStartColumn = model.RangeStartColumn,
            RangeEndColumn = model.RangeEndColumn,
            LabelColumn = model.LabelColumn,
            ValueColumn = model.ValueColumn,
            GroupByColumn = model.GroupByColumn,
            ShowLegend = model.ShowLegend,
            LineInterpolationMode = model.LineInterpolationMode
        };

        visualization.Name = model.Name.Trim();
        visualization.Type = model.Type;
        visualization.ConfigJson = JsonSerializer.Serialize(config);
        visualization.IsAutoRefreshEnabled = model.IsAutoRefreshEnabled;
        visualization.AutoRefreshIntervalSeconds = model.IsAutoRefreshEnabled ? model.AutoRefreshIntervalSeconds : null;
        visualization.UpdatedAt = DateTimeOffset.UtcNow;

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

        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => d.QueryId == visualization.QueryId)
            .ToListAsync();
        var parameterValues = await GetLatestParameterValuesAsync(visualization.QueryId);

        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = visualization.Query.SqlText,
            Definitions = definitions,
            Values = parameterValues,
            AllowText = true
        });

        QueryResultViewModel result;
        if (!applyResult.Success)
        {
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        }
        var config = JsonSerializer.Deserialize<VisualizationConfig>(visualization.ConfigJson) ?? new VisualizationConfig();

        var dashboards = await _dbContext.Dashboards
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = d.Name
            })
            .ToListAsync();

        var model = new VisualizationDetailsViewModel
        {
            Visualization = visualization,
            Result = result,
            Config = config,
            QueryName = visualization.Query.Name,
            Dashboards = dashboards
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToDashboard(AddVisualizationToDashboardInputModel model)
    {
        var visualization = await _dbContext.Visualizations
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == model.VisualizationId);
        if (visualization == null)
        {
            return NotFound();
        }

        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == model.DashboardId);
        if (dashboard == null)
        {
            return NotFound();
        }

        var widgetHeights = await _dbContext.DashboardWidgets
            .Where(w => w.DashboardId == model.DashboardId)
            .Select(w => w.Y + w.Height)
            .ToListAsync();
        var maxY = widgetHeights.Count > 0 ? widgetHeights.Max() : 0;

        var widget = new DashboardWidget
        {
            DashboardId = model.DashboardId,
            VisualizationId = model.VisualizationId,
            X = 0,
            Y = maxY,
            Width = 4,
            Height = 4
        };
        _dbContext.DashboardWidgets.Add(widget);
        dashboard.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Visualization added to dashboard.";
        return RedirectToAction("Edit", "Dashboards", new { id = model.DashboardId });
    }

    [HttpGet("/visualizations/data/{id}")]
    public async Task<IActionResult> Data(int id)
    {
        var visualization = await _dbContext.Visualizations
            .Include(v => v.Query)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (visualization == null || visualization.Query == null)
        {
            return NotFound();
        }

        _logger.LogInformation("Auto-refresh visualization {VisualizationId}", id);
        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => d.QueryId == visualization.QueryId)
            .ToListAsync();
        var parameterValues = await GetLatestParameterValuesAsync(visualization.QueryId);

        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = visualization.Query.SqlText,
            Definitions = definitions,
            Values = parameterValues,
            AllowText = true
        });

        if (!applyResult.Success)
        {
            return Ok(new
            {
                success = false,
                errorMessage = string.Join(" ", applyResult.Errors)
            });
        }

        var result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Refresh failed."
            });
        }

        return Ok(new
        {
            success = true,
            columns = result.Columns,
            rows = result.Rows
        });
    }

    private async Task<IReadOnlyList<QueryParameterDefinitionViewModel>> BuildParameterDefinitionsAsync(int queryId)
    {
        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => d.QueryId == queryId)
            .OrderBy(d => d.Name)
            .ToListAsync();

        var definitionModels = new List<QueryParameterDefinitionViewModel>();
        foreach (var definition in definitions)
        {
            definitionModels.Add(new QueryParameterDefinitionViewModel
            {
                Id = definition.Id,
                Name = definition.Name,
                Title = string.IsNullOrWhiteSpace(definition.Title) ? definition.Name : definition.Title,
                Type = definition.Type,
                DefaultValue = definition.DefaultValue,
                IsRequired = definition.IsRequired,
                SettingsJson = definition.SettingsJson ?? "{}",
                Options = await BuildOptionsAsync(definition)
            });
        }

        return definitionModels;
    }

    private async Task<Dictionary<string, string>> GetLatestParameterValuesAsync(int queryId)
    {
        var latest = await _dbContext.QueryExecutions
            .AsNoTracking()
            .Where(e => e.QueryId == queryId && e.ParametersJson != null)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => e.ParametersJson)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(latest))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(latest);
            return values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<IReadOnlyList<QueryParameterOptionViewModel>> BuildOptionsAsync(QueryParameterDefinition definition)
    {
        var options = new List<QueryParameterOptionViewModel>();
        if (string.IsNullOrWhiteSpace(definition.SettingsJson))
        {
            return options;
        }

        using var document = JsonDocument.Parse(definition.SettingsJson);
        if (document.RootElement.TryGetProperty("options", out var optionsElement) &&
            optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in optionsElement.EnumerateArray())
            {
                if (!option.TryGetProperty("value", out var valueElement))
                {
                    continue;
                }

                var value = valueElement.GetString();
                var label = option.TryGetProperty("label", out var labelElement)
                    ? labelElement.GetString()
                    : value;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    options.Add(new QueryParameterOptionViewModel
                    {
                        Label = label ?? value!,
                        Value = value!
                    });
                }
            }

            return options;
        }

        if (document.RootElement.TryGetProperty("sourceQueryId", out var queryIdElement) &&
            queryIdElement.TryGetInt32(out var queryId))
        {
            var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queryId);
            if (query == null)
            {
                return options;
            }

            var result = await _queryRunner.RunAsync(query.DataSourceId, query.SqlText);
            if (!result.Success)
            {
                return options;
            }

            foreach (var row in result.Rows)
            {
                if (row.Count == 0)
                {
                    continue;
                }

                var value = row.Count > 1 ? row[1] : row[0];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    options.Add(new QueryParameterOptionViewModel
                    {
                        Label = row[0] ?? value,
                        Value = value
                    });
                }
            }
        }

        return options;
    }
}
