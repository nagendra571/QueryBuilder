using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Dashboards;
using QueryBuilder.Web.Models.Queries;
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
    private readonly IQueryParameterService _parameterService;

    public PublicController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        PublicShareService publicShareService,
        ILogger<PublicController> logger,
        IQueryParameterService parameterService)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _publicShareService = publicShareService;
        _logger = logger;
        _parameterService = parameterService;
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

        var parameterControls = await _dbContext.DashboardParameterControls
            .AsNoTracking()
            .Where(c => c.DashboardId == share.EntityId)
            .ToListAsync();
        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => d.QueryId == widget.Visualization.QueryId)
            .ToListAsync();
        var mappings = await _dbContext.DashboardParameterMappings
            .AsNoTracking()
            .Where(m => m.DashboardWidgetId == widgetId)
            .ToListAsync();
        var parameterState = BuildDashboardParameterState(Request.Query, parameterControls, new[] { widget }, definitions);
        var appliedValues = BuildWidgetParameterValues(widget.Id, definitions, mappings, parameterControls, parameterState);

        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = widget.Visualization.Query.SqlText,
            Definitions = definitions,
            Values = appliedValues,
            AllowText = false
        });

        if (!applyResult.Success)
        {
            return BadRequest(new ParameterValidationErrorResponse
            {
                Message = "One or more parameter values are missing or invalid.",
                Errors = applyResult.Errors,
                ErrorMessage = "Refresh failed."
            });
        }

        var result = await _queryRunner.RunAsync(widget.Visualization.Query.DataSourceId, applyResult.Sql);
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

        var parameterControls = await _dbContext.DashboardParameterControls
            .AsNoTracking()
            .Where(c => c.DashboardId == dashboardId)
            .ToListAsync();
        var widgetMappings = await _dbContext.DashboardParameterMappings
            .AsNoTracking()
            .Where(m => widgets.Select(w => w.Id).Contains(m.DashboardWidgetId))
            .ToListAsync();
        var queryIds = widgets
            .Where(w => w.Visualization != null)
            .Select(w => w.Visualization!.QueryId)
            .Distinct()
            .ToList();
        var queryDefinitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => queryIds.Contains(d.QueryId))
            .ToListAsync();

        var parameterState = BuildDashboardParameterState(Request.Query, parameterControls, widgets, queryDefinitions);

        var viewWidgets = new List<DashboardWidgetViewModel>();
        foreach (var widget in widgets)
        {
            if (widget.Visualization == null || widget.Visualization.Query == null)
            {
                continue;
            }

            var definitions = queryDefinitions
                .Where(d => d.QueryId == widget.Visualization.QueryId)
                .ToList();
            var mappings = widgetMappings
                .Where(m => m.DashboardWidgetId == widget.Id)
                .ToList();
            var appliedValues = BuildWidgetParameterValues(widget.Id, definitions, mappings, parameterControls, parameterState);

            var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
            {
                Sql = widget.Visualization.Query.SqlText,
                Definitions = definitions,
                Values = appliedValues,
                AllowText = false
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
                result = await _queryRunner.RunAsync(widget.Visualization.Query.DataSourceId, applyResult.Sql);
            }
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
            Widgets = viewWidgets,
            ParameterControls = parameterControls
                .Where(c => c.Type != QueryParameterType.Text)
                .Select(c => new DashboardParameterControlViewModel
                {
                    Id = c.Id,
                    Key = c.Key,
                    Title = c.Title,
                    Type = c.Type,
                    DefaultValue = c.DefaultValue,
                    Placement = c.Placement,
                    SettingsJson = c.SettingsJson
                })
                .ToList(),
            WidgetParameters = await BuildWidgetParameterViewModelsAsync(dashboardId, includeText: false),
            ParameterState = parameterState
        };
    }

    private async Task<IReadOnlyList<DashboardWidgetParameterViewModel>> BuildWidgetParameterViewModelsAsync(int dashboardId, bool includeText)
    {
        var widgets = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.DashboardId == dashboardId)
            .Include(w => w.Visualization)
            .ThenInclude(v => v!.Query)
            .ToListAsync();

        if (widgets.Count == 0)
        {
            return Array.Empty<DashboardWidgetParameterViewModel>();
        }

        var queryIds = widgets.Where(w => w.Visualization != null).Select(w => w.Visualization!.QueryId).Distinct().ToList();
        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => queryIds.Contains(d.QueryId))
            .ToListAsync();

        if (!includeText)
        {
            definitions = definitions.Where(d => d.Type != QueryParameterType.Text).ToList();
        }

        var mappings = await _dbContext.DashboardParameterMappings
            .AsNoTracking()
            .Where(m => widgets.Select(w => w.Id).Contains(m.DashboardWidgetId))
            .ToListAsync();

        var results = new List<DashboardWidgetParameterViewModel>();
        foreach (var widget in widgets)
        {
            if (widget.Visualization?.Query == null)
            {
                continue;
            }

            var widgetDefinitions = definitions
                .Where(d => d.QueryId == widget.Visualization.QueryId)
                .ToList();

            var definitionModels = new List<QueryParameterDefinitionViewModel>();
            foreach (var definition in widgetDefinitions)
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

            var mappingModels = mappings
                .Where(m => m.DashboardWidgetId == widget.Id)
                .Select(m => new DashboardParameterMappingViewModel
                {
                    QueryParamKey = m.QueryParamKey,
                    BindingType = m.BindingType,
                    ControlKey = m.ControlKey,
                    StaticValue = m.StaticValue
                })
                .ToList();

            results.Add(new DashboardWidgetParameterViewModel
            {
                WidgetId = widget.Id,
                VisualizationId = widget.Visualization.Id,
                QueryId = widget.Visualization.QueryId,
                VisualizationName = widget.Visualization.Name,
                Parameters = definitionModels,
                Mappings = mappingModels
            });
        }

        return results;
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

    private static DashboardParameterStateViewModel BuildDashboardParameterState(
        IQueryCollection query,
        IReadOnlyList<DashboardParameterControl> controls,
        IReadOnlyList<DashboardWidget> widgets,
        IReadOnlyList<QueryParameterDefinition> definitions)
    {
        var dashboardControlValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in controls)
        {
            if (control.Type is QueryParameterType.DateRange or QueryParameterType.DateTimeRange)
            {
                var start = query[$"dc_{control.Key}_start"].ToString();
                var end = query[$"dc_{control.Key}_end"].ToString();
                if (!string.IsNullOrWhiteSpace(start))
                {
                    dashboardControlValues[$"{control.Key}.start"] = start;
                }
                if (!string.IsNullOrWhiteSpace(end))
                {
                    dashboardControlValues[$"{control.Key}.end"] = end;
                }
            }
            else
            {
                var value = query[$"dc_{control.Key}"].ToString();
                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(control.DefaultValue))
                {
                    value = control.DefaultValue;
                }
                if (!string.IsNullOrWhiteSpace(value))
                {
                    dashboardControlValues[control.Key] = value;
                }
            }
        }

        var inlineValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var widget in widgets)
        {
            var widgetDefinitions = definitions
                .Where(d => d.QueryId == widget.Visualization?.QueryId)
                .ToList();

            foreach (var definition in widgetDefinitions)
            {
                if (definition.Type is QueryParameterType.DateRange or QueryParameterType.DateTimeRange)
                {
                    var start = query[$"wp_{widget.Id}_{definition.Name}_start"].ToString();
                    var end = query[$"wp_{widget.Id}_{definition.Name}_end"].ToString();
                    if (!string.IsNullOrWhiteSpace(start))
                    {
                        inlineValues[$"{widget.Id}:{definition.Name}.start"] = start;
                    }
                    if (!string.IsNullOrWhiteSpace(end))
                    {
                        inlineValues[$"{widget.Id}:{definition.Name}.end"] = end;
                    }
                }
                else
                {
                    var value = query[$"wp_{widget.Id}_{definition.Name}"].ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        inlineValues[$"{widget.Id}:{definition.Name}"] = value;
                    }
                }
            }
        }

        return new DashboardParameterStateViewModel
        {
            DashboardControlValues = dashboardControlValues,
            InlineValues = inlineValues
        };
    }

    private static Dictionary<string, string> BuildWidgetParameterValues(
        int widgetId,
        IReadOnlyList<QueryParameterDefinition> definitions,
        IReadOnlyList<DashboardParameterMapping> mappings,
        IReadOnlyList<DashboardParameterControl> controls,
        DashboardParameterStateViewModel parameterState)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var mapping = mappings.FirstOrDefault(m =>
                m.QueryParamKey.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));

            if (mapping?.BindingType == DashboardParameterBindingType.StaticValue)
            {
                ApplyStaticValue(values, definition, mapping.StaticValue);
                continue;
            }

            if (mapping?.BindingType == DashboardParameterBindingType.DashboardControlKey)
            {
                var controlKey = mapping.ControlKey;
                var control = controls.FirstOrDefault(c => c.Key.Equals(controlKey, StringComparison.OrdinalIgnoreCase));
                if (control == null)
                {
                    continue;
                }

                if (definition.Type is QueryParameterType.DateRange or QueryParameterType.DateTimeRange)
                {
                    if (parameterState.DashboardControlValues.TryGetValue($"{control.Key}.start", out var start))
                    {
                        values[$"{definition.Name}.start"] = start;
                    }
                    if (parameterState.DashboardControlValues.TryGetValue($"{control.Key}.end", out var end))
                    {
                        values[$"{definition.Name}.end"] = end;
                    }
                }
                else if (parameterState.DashboardControlValues.TryGetValue(control.Key, out var controlValue))
                {
                    values[definition.Name] = controlValue;
                }

                continue;
            }

            if (definition.Type is QueryParameterType.DateRange or QueryParameterType.DateTimeRange)
            {
                if (parameterState.InlineValues.TryGetValue($"{widgetId}:{definition.Name}.start", out var start))
                {
                    values[$"{definition.Name}.start"] = start;
                }
                if (parameterState.InlineValues.TryGetValue($"{widgetId}:{definition.Name}.end", out var end))
                {
                    values[$"{definition.Name}.end"] = end;
                }
            }
            else if (parameterState.InlineValues.TryGetValue($"{widgetId}:{definition.Name}", out var inlineValue))
            {
                values[definition.Name] = inlineValue;
            }
        }

        return values;
    }

    private static void ApplyStaticValue(
        Dictionary<string, string> values,
        QueryParameterDefinition definition,
        string? staticValue)
    {
        if (definition.Type is QueryParameterType.DateRange or QueryParameterType.DateTimeRange)
        {
            var (start, end) = ParseRangeStaticValue(staticValue);
            if (!string.IsNullOrWhiteSpace(start))
            {
                values[$"{definition.Name}.start"] = start;
            }
            if (!string.IsNullOrWhiteSpace(end))
            {
                values[$"{definition.Name}.end"] = end;
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(staticValue))
        {
            values[definition.Name] = staticValue;
        }
    }

    private static (string? start, string? end) ParseRangeStaticValue(string? staticValue)
    {
        if (string.IsNullOrWhiteSpace(staticValue))
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(staticValue);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var start = doc.RootElement.TryGetProperty("start", out var startElement) ? startElement.GetString() : null;
                var end = doc.RootElement.TryGetProperty("end", out var endElement) ? endElement.GetString() : null;
                return (start, end);
            }
        }
        catch (JsonException)
        {
        }

        if (staticValue.Contains('|'))
        {
            var parts = staticValue.Split('|', 2, StringSplitOptions.TrimEntries);
            return (parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1));
        }

        return (staticValue, null);
    }
}
