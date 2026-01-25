using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly IVisualizationService _visualizationService;
    private readonly PermissionService _permissionService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TableVisualizationConfigBuilder _tableConfigBuilder;
    private static readonly HashSet<int> AllowedRefreshIntervals = new()
    {
        30, 60, 300, 600, 1800, 3600
    };
    private static readonly JsonSerializerOptions TableConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly int[] AllowedPageSizes = { 25, 50, 100, 250, 500 };

    public VisualizationsController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        ILogger<VisualizationsController> logger,
        IQueryParameterService parameterService,
        IVisualizationService visualizationService,
        PermissionService permissionService,
        UserManager<IdentityUser> userManager,
        TableVisualizationConfigBuilder tableConfigBuilder)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _logger = logger;
        _parameterService = parameterService;
        _visualizationService = visualizationService;
        _permissionService = permissionService;
        _userManager = userManager;
        _tableConfigBuilder = tableConfigBuilder;
    }

    public async Task<IActionResult> Create(int queryId, int? executionId)
    {
        var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queryId);
        if (query == null)
        {
            return NotFound();
        }

        var definitions = await BuildParameterDefinitionsAsync(query.Id, query.SqlText);
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
            var generalError = applyResult.Errors.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Parameter));
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = generalError?.Message,
                ParameterErrors = applyResult.Errors
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
            ParameterValues = parameterValues,
            LatestExecutionId = await ResolveExecutionIdAsync(query.Id, executionId),
            TableConfigJson = JsonSerializer.Serialize(_tableConfigBuilder.Build(result.Columns, result.Rows), TableConfigJsonOptions)
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

        var definitions = await BuildParameterDefinitionsAsync(query.Id, query.SqlText);
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
            var generalError = applyResult.Errors.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Parameter));
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = generalError?.Message,
                ParameterErrors = applyResult.Errors
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(query.DataSourceId, applyResult.Sql);
        }
        model.Columns = result.Columns;
        model.LatestExecutionId = await GetLatestExecutionIdAsync(query.Id);
        model.Result = string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase) ? result : null;
        if (model.YColumns.Count == 0 && !string.IsNullOrWhiteSpace(model.YColumn))
        {
            model.YColumns.Add(model.YColumn);
        }

        TableVisualizationConfig? tableConfig = null;
        if (model.Type == VisualizationType.Table)
        {
            tableConfig = BuildTableConfig(model, result);
            model.TableConfigJson = JsonSerializer.Serialize(tableConfig, TableConfigJsonOptions);
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

        var configJson = model.Type == VisualizationType.Table
            ? JsonSerializer.Serialize(tableConfig ?? BuildTableConfig(model, result), TableConfigJsonOptions)
            : JsonSerializer.Serialize(new VisualizationConfig
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
            });

        var visualization = new Visualization
        {
            QueryId = model.QueryId,
            Name = model.Name.Trim(),
            Type = model.Type,
            ConfigJson = configJson,
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

        var definitions = await BuildParameterDefinitionsAsync(visualization.QueryId, visualization.Query.SqlText);
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
            var generalError = applyResult.Errors.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Parameter));
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = generalError?.Message,
                ParameterErrors = applyResult.Errors
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        }
        var config = JsonSerializer.Deserialize<VisualizationConfig>(visualization.ConfigJson) ?? new VisualizationConfig();
        TableVisualizationConfig? tableConfig = null;
        if (visualization.Type == VisualizationType.Table)
        {
            tableConfig = TryDeserializeTableConfig(visualization.ConfigJson);
            tableConfig = _tableConfigBuilder.Build(result.Columns, result.Rows, tableConfig);
        }
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
            ParameterValues = parameterValues,
            CanDelete = await CanDeleteVisualizationAsync(visualization.Query),
            LatestExecutionId = await GetLatestExecutionIdAsync(visualization.QueryId),
            TableConfigJson = tableConfig != null ? JsonSerializer.Serialize(tableConfig, TableConfigJsonOptions) : null
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

        var definitions = await BuildParameterDefinitionsAsync(query.Id, query.SqlText);
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
            var generalError = applyResult.Errors.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Parameter));
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = generalError?.Message,
                ParameterErrors = applyResult.Errors
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(query.DataSourceId, applyResult.Sql);
        }
        model.Columns = result.Columns;
        model.LatestExecutionId = await GetLatestExecutionIdAsync(query.Id);
        model.Result = string.Equals(model.SubmitAction, "preview", StringComparison.OrdinalIgnoreCase) ? result : null;
        if (model.YColumns.Count == 0 && !string.IsNullOrWhiteSpace(model.YColumn))
        {
            model.YColumns.Add(model.YColumn);
        }
        model.CanDelete = await CanDeleteVisualizationAsync(query);

        TableVisualizationConfig? tableConfig = null;
        if (model.Type == VisualizationType.Table)
        {
            tableConfig = BuildTableConfig(model, result);
            model.TableConfigJson = JsonSerializer.Serialize(tableConfig, TableConfigJsonOptions);
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

        var configJson = model.Type == VisualizationType.Table
            ? JsonSerializer.Serialize(tableConfig ?? BuildTableConfig(model, result), TableConfigJsonOptions)
            : JsonSerializer.Serialize(new VisualizationConfig
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
            });

        visualization.Name = model.Name.Trim();
        visualization.Type = model.Type;
        visualization.ConfigJson = configJson;
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
            var generalError = applyResult.Errors.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Parameter));
            result = new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = generalError?.Message,
                ParameterErrors = applyResult.Errors
            };
        }
        else
        {
            result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        }
        var config = JsonSerializer.Deserialize<VisualizationConfig>(visualization.ConfigJson) ?? new VisualizationConfig();
        TableVisualizationConfig? tableConfig = null;
        if (visualization.Type == VisualizationType.Table)
        {
            tableConfig = TryDeserializeTableConfig(visualization.ConfigJson);
            tableConfig = _tableConfigBuilder.Build(result.Columns, result.Rows, tableConfig);
        }

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
            TableConfig = tableConfig,
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
            return BadRequest(new ParameterValidationErrorResponse
            {
                Message = "One or more parameter values are missing or invalid.",
                Errors = applyResult.Errors,
                ErrorMessage = "Parameter validation failed."
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

    [HttpGet("/visualizations/{id}/results")]
    public async Task<IActionResult> ResultsPage(int id, int page = 1, int pageSize = 25)
    {
        var visualization = await _dbContext.Visualizations
            .Include(v => v.Query)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (visualization == null || visualization.Query == null)
        {
            return NotFound();
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = AllowedPageSizes.Contains(pageSize) ? pageSize : 25;

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
            return BadRequest(new ParameterValidationErrorResponse
            {
                Message = "One or more parameter values are missing or invalid.",
                Errors = applyResult.Errors,
                ErrorMessage = "Parameter validation failed."
            });
        }

        var result = await _queryRunner.RunAsync(visualization.Query.DataSourceId, applyResult.Sql);
        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Failed to load results."
            });
        }

        var start = (normalizedPage - 1) * normalizedSize;
        var pageRows = result.Rows.Skip(start).Take(normalizedSize).ToList();
        var hasNext = result.Rows.Count > start + pageRows.Count;

        return Ok(new
        {
            success = true,
            visualizationId = visualization.Id,
            queryId = visualization.QueryId,
            page = normalizedPage,
            pageSize = normalizedSize,
            hasNext,
            columns = result.Columns.Select(name => new { name }),
            rows = pageRows.Select(row =>
            {
                var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < result.Columns.Count; i++)
                {
                    map[result.Columns[i]] = i < row.Count ? row[i] : null;
                }
                return map;
            })
        });
    }

    [HttpGet("/visualizations/execution-preview/{executionId}")]
    public async Task<IActionResult> ExecutionPreview(int executionId)
    {
        if (executionId <= 0)
        {
            return BadRequest();
        }

        var execution = await _dbContext.QueryExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId);
        if (execution == null)
        {
            return NotFound();
        }

        if (!await _permissionService.CanViewQueryAsync(User, execution.QueryId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(execution.ResultJson))
        {
            return Ok(new { success = false, errorMessage = "No stored results available." });
        }

        QueryExecutionResult? payload;
        try
        {
            payload = JsonSerializer.Deserialize<QueryExecutionResult>(execution.ResultJson);
        }
        catch (JsonException)
        {
            return Ok(new { success = false, errorMessage = "Stored results could not be read." });
        }

        return Ok(new
        {
            success = true,
            columns = payload?.Columns ?? Array.Empty<string>(),
            rows = payload?.Rows ?? Array.Empty<IReadOnlyList<string?>>()
        });
    }

    private async Task<bool> CanDeleteVisualizationAsync(Query query)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        if (!User.IsInRole("Editor"))
        {
            return false;
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (query.CreatedById == userId)
        {
            return true;
        }

        return await _permissionService.HasQueryEditAccessAsync(userId, query.Id);
    }

    private async Task<int?> ResolveExecutionIdAsync(int queryId, int? executionId)
    {
        if (executionId.HasValue)
        {
            var matchesQuery = await _dbContext.QueryExecutions
                .AsNoTracking()
                .AnyAsync(e => e.Id == executionId.Value && e.QueryId == queryId);
            if (matchesQuery)
            {
                return executionId.Value;
            }
        }

        return await GetLatestExecutionIdAsync(queryId);
    }

    private async Task<int?> GetLatestExecutionIdAsync(int queryId)
    {
        return await _dbContext.QueryExecutions
            .AsNoTracking()
            .Where(e => e.QueryId == queryId && e.Status == QueryExecutionStatus.Success && e.ResultJson != null)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();
    }

    private TableVisualizationConfig BuildTableConfig(VisualizationEditViewModel model, QueryResultViewModel result)
    {
        var existing = TryDeserializeTableConfig(model.TableConfigJson);
        return _tableConfigBuilder.Build(result.Columns, result.Rows, existing);
    }

    private static TableVisualizationConfig? TryDeserializeTableConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TableVisualizationConfig>(json, TableConfigJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [HttpPost("visualizations/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _visualizationService.DeleteVisualizationAsync(id, User);
        if (result.IsForbidden)
        {
            return Forbid();
        }

        return Ok(result);
    }

    private async Task<IReadOnlyList<QueryParameterDefinitionViewModel>> BuildParameterDefinitionsAsync(int queryId, string sqlText)
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

        var tokenInfo = QueryParameterParser.ExtractTokens(sqlText)
            .Select(token =>
            {
                var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                return new
                {
                    Name = parts[0],
                    Suffix = parts.Length == 2 ? parts[1] : null
                };
            })
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Any(t => string.Equals(t.Suffix, "start", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(t.Suffix, "end", StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokenInfo)
        {
            if (definitionModels.Any(d => string.Equals(d.Name, token.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            definitionModels.Add(new QueryParameterDefinitionViewModel
            {
                Id = 0,
                Name = token.Key,
                Title = token.Key,
                Type = token.Value ? QueryParameterType.DateRange : QueryParameterType.Text,
                DefaultValue = null,
                IsRequired = false,
                SettingsJson = "{}",
                Options = new List<QueryParameterOptionViewModel>()
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
