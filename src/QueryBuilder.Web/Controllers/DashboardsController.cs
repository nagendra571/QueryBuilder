using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Dashboards;
using QueryBuilder.Web.Models.PublicShares;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Sharing;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[Authorize]
public class DashboardsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QueryRunner _queryRunner;
    private readonly PermissionService _permissionService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly PublicShareService _publicShareService;
    private readonly ILogger<DashboardsController> _logger;
    private readonly IQueryParameterService _parameterService;
    private readonly TableVisualizationConfigBuilder _tableConfigBuilder;
    private readonly ChartVisualizationDataBuilder _chartDataBuilder;
    private readonly CounterVisualizationDataBuilder _counterDataBuilder;
    private static readonly JsonSerializerOptions TableConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions ChartConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions CounterConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public DashboardsController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        PermissionService permissionService,
        UserManager<IdentityUser> userManager,
        PublicShareService publicShareService,
        ILogger<DashboardsController> logger,
        IQueryParameterService parameterService,
        TableVisualizationConfigBuilder tableConfigBuilder,
        ChartVisualizationDataBuilder chartDataBuilder,
        CounterVisualizationDataBuilder counterDataBuilder)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _permissionService = permissionService;
        _userManager = userManager;
        _publicShareService = publicShareService;
        _logger = logger;
        _parameterService = parameterService;
        _tableConfigBuilder = tableConfigBuilder;
        _chartDataBuilder = chartDataBuilder;
        _counterDataBuilder = counterDataBuilder;
    }

    public async Task<IActionResult> Index()
    {
        IQueryable<Dashboard> queryable = _dbContext.Dashboards
            .AsNoTracking();

        if (!User.IsInRole("Admin") && !User.IsInRole("Editor"))
        {
            var allowedIds = await _permissionService.GetAccessibleDashboardIdsAsync(User);
            if (allowedIds.Count == 0)
            {
                return View(new List<DashboardListItemViewModel>());
            }

            queryable = queryable.Where(d => allowedIds.Contains(d.Id));
        }

        var dashboards = await queryable
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DashboardListItemViewModel
            {
                Id = d.Id,
                Name = d.Name,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return View(dashboards);
    }

    [Authorize(Roles = "Admin,Editor")]
    public IActionResult Create()
    {
        return View(new DashboardEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create(DashboardEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var now = DateTimeOffset.UtcNow;
        var dashboard = new Dashboard
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Dashboards.Add(dashboard);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = dashboard.Id });
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id)
    {
        var dashboard = await _dbContext.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard == null)
        {
            return NotFound();
        }

        var widgets = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.DashboardId == dashboard.Id)
            .Include(w => w.Visualization)
            .Select(w => new DashboardWidgetItemViewModel
            {
                Id = w.Id,
                VisualizationId = w.VisualizationId,
                VisualizationName = w.Visualization != null ? w.Visualization.Name : "Visualization",
                VisualizationType = w.Visualization != null ? w.Visualization.Type : VisualizationType.Table,
                X = w.X,
                Y = w.Y,
                Width = w.Width,
                Height = w.Height
            })
            .ToListAsync();

        var visuals = await _dbContext.Visualizations
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync();

        var model = new DashboardEditViewModel
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
            Widgets = widgets,
            AvailableVisualizations = visuals,
            ShareSection = await BuildShareSectionAsync(ShareEntityType.Dashboard, dashboard.Id),
            ParameterControls = await BuildDashboardParameterControlsAsync(dashboard.Id),
            WidgetParameters = await BuildWidgetParameterViewModelsAsync(dashboard.Id)
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> VisualizationPickerData()
    {
        var queries = await _dbContext.Queries
            .AsNoTracking()
            .OrderBy(q => q.Name)
            .Select(q => new { q.Id, q.Name })
            .ToListAsync();

        var visualizations = await _dbContext.Visualizations
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new { v.Id, v.Name, Type = v.Type.ToString(), v.QueryId })
            .ToListAsync();

        return Ok(new { success = true, queries, visualizations });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> VisualizationParameters(int visualizationId)
    {
        if (visualizationId <= 0)
        {
            return BadRequest();
        }

        var visualization = await _dbContext.Visualizations
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == visualizationId);
        if (visualization == null)
        {
            return NotFound();
        }

        var definitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => d.QueryId == visualization.QueryId)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Name, d.Title, Type = d.Type.ToString() })
            .ToListAsync();

        return Ok(new { success = true, queryId = visualization.QueryId, parameters = definitions });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddWidgetFromPicker([FromBody] AddDashboardWidgetRequestModel model)
    {
        if (model == null)
        {
            return BadRequest();
        }

        if (model.DashboardId <= 0 || model.VisualizationId <= 0)
        {
            return BadRequest();
        }

        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == model.DashboardId);
        if (dashboard == null)
        {
            return NotFound();
        }

        var visualization = await _dbContext.Visualizations
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == model.VisualizationId);
        if (visualization == null)
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

        if (model.Mappings.Count > 0)
        {
            var definitions = await _dbContext.QueryParameterDefinitions
                .AsNoTracking()
                .Where(d => d.QueryId == visualization.QueryId)
                .ToListAsync();
            var existingControls = await _dbContext.DashboardParameterControls
                .Where(c => c.DashboardId == model.DashboardId)
                .ToListAsync();

            foreach (var input in model.Mappings)
            {
                var definition = definitions.FirstOrDefault(d =>
                    d.Name.Equals(input.QueryParamKey, StringComparison.OrdinalIgnoreCase));
                if (definition == null)
                {
                    continue;
                }

                if (input.BindingType == DashboardParameterBindingType.DashboardControlKey)
                {
                    var controlKey = input.ControlKey?.Trim();
                    if (string.IsNullOrWhiteSpace(controlKey))
                    {
                        continue;
                    }

                    if (input.CreateControl &&
                        existingControls.All(c => !c.Key.Equals(controlKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        var now = DateTimeOffset.UtcNow;
                        var title = string.IsNullOrWhiteSpace(input.ControlTitle) ? controlKey : input.ControlTitle.Trim();
                        var control = new DashboardParameterControl
                        {
                            DashboardId = model.DashboardId,
                            Key = controlKey,
                            Title = title,
                            Type = definition.Type,
                            DefaultValue = definition.DefaultValue,
                            Placement = DashboardParameterPlacement.TopBar,
                            SettingsJson = definition.SettingsJson,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _dbContext.DashboardParameterControls.Add(control);
                        existingControls.Add(control);
                    }
                }

                var staticValue = input.StaticValue?.Trim();
                if (definition.Type == QueryParameterType.DateRange || definition.Type == QueryParameterType.DateTimeRange)
                {
                    if (!string.IsNullOrWhiteSpace(input.StaticValueStart) || !string.IsNullOrWhiteSpace(input.StaticValueEnd))
                    {
                        staticValue = JsonSerializer.Serialize(new
                        {
                            start = input.StaticValueStart?.Trim(),
                            end = input.StaticValueEnd?.Trim()
                        });
                    }
                }

                _dbContext.DashboardParameterMappings.Add(new DashboardParameterMapping
                {
                    DashboardWidgetId = widget.Id,
                    QueryId = input.QueryId,
                    QueryParamKey = input.QueryParamKey.Trim(),
                    BindingType = input.BindingType,
                    ControlKey = input.BindingType == DashboardParameterBindingType.DashboardControlKey ? input.ControlKey?.Trim() : null,
                    StaticValue = input.BindingType == DashboardParameterBindingType.StaticValue ? staticValue : null
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        return Ok(new
        {
            success = true,
            widget = new
            {
                id = widget.Id,
                visualizationId = visualization.Id,
                visualizationName = visualization.Name,
                visualizationType = visualization.Type.ToString(),
                x = widget.X,
                y = widget.Y,
                width = widget.Width,
                height = widget.Height
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SaveLayout(int id, [FromBody] DashboardSaveLayoutRequest request)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard == null)
        {
            return NotFound();
        }

        var existingWidgets = await _dbContext.DashboardWidgets
            .Where(w => w.DashboardId == id)
            .ToListAsync();

        var keepIds = new HashSet<int>();
        foreach (var widget in request.Widgets)
        {
            if (widget.Id.HasValue)
            {
                var existing = existingWidgets.FirstOrDefault(w => w.Id == widget.Id.Value);
                if (existing != null)
                {
                    existing.X = widget.X;
                    existing.Y = widget.Y;
                    existing.Width = widget.Width;
                    existing.Height = widget.Height;
                    keepIds.Add(existing.Id);
                }
            }
            else
            {
                var newWidget = new DashboardWidget
                {
                    DashboardId = id,
                    VisualizationId = widget.VisualizationId,
                    X = widget.X,
                    Y = widget.Y,
                    Width = widget.Width,
                    Height = widget.Height
                };
                _dbContext.DashboardWidgets.Add(newWidget);
            }
        }

        foreach (var existing in existingWidgets)
        {
            if (!keepIds.Contains(existing.Id) && !request.Widgets.Any(w => w.Id == existing.Id))
            {
                _dbContext.DashboardWidgets.Remove(existing);
            }
        }

        dashboard.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    public async Task<IActionResult> View(int id)
    {
        if (!await _permissionService.CanViewDashboardAsync(User, id))
        {
            return Forbid();
        }

        var dashboard = await _dbContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard == null)
        {
            return NotFound();
        }

        var widgets = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.DashboardId == id)
            .Include(w => w.Visualization)
            .ThenInclude(v => v!.Query)
            .ToListAsync();

        var parameterControls = await _dbContext.DashboardParameterControls
            .AsNoTracking()
            .Where(c => c.DashboardId == id)
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

            var allowText = User.IsInRole("Admin") || User.IsInRole("Editor");
            var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
            {
                Sql = widget.Visualization.Query.SqlText,
                Definitions = definitions,
                Values = appliedValues,
                AllowText = allowText
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
            var chartConfig = TryDeserializeChartConfig(widget.Visualization.ConfigJson) ?? ChartVisualizationConfig.CreateDefault();
            var counterConfig = TryDeserializeCounterConfig(widget.Visualization.ConfigJson) ?? CounterVisualizationConfig.CreateDefault();
            TableVisualizationConfig? tableConfig = null;
            if (widget.Visualization.Type == VisualizationType.Table)
            {
                tableConfig = TryDeserializeTableConfig(widget.Visualization.ConfigJson);
                tableConfig = _tableConfigBuilder.Build(result.Columns, result.Rows, tableConfig);
            }
            ChartVisualizationRenderModel? chartRender = null;
            CounterVisualizationRenderModel? counterRender = null;
            if (widget.Visualization.Type == VisualizationType.Counter)
            {
                counterRender = _counterDataBuilder.Build(counterConfig, result.Columns, result.Rows);
            }
            else if (widget.Visualization.Type != VisualizationType.Table)
            {
                chartRender = _chartDataBuilder.Build(chartConfig, result.Columns, result.Rows);
            }

            viewWidgets.Add(new DashboardWidgetViewModel
            {
                Widget = widget,
                Visualization = widget.Visualization,
                ChartConfig = chartConfig,
                ChartRender = chartRender,
                CounterConfig = counterConfig,
                CounterRender = counterRender,
                TableConfig = tableConfig,
                Result = result
            });
        }

        var share = await _publicShareService.GetActiveShareAsync(PublicShareEntityType.Dashboard, id);
        return View(new DashboardViewModel
        {
            Dashboard = dashboard,
            Widgets = viewWidgets,
            PublicShareToken = share?.Token,
            PublicShareEnabled = share?.IsEnabled ?? false,
            PublicShareExpiresAt = share?.ExpiresAt,
            ParameterControls = parameterControls.Select(c => new DashboardParameterControlViewModel
            {
                Id = c.Id,
                Key = c.Key,
                Title = c.Title,
                Type = c.Type,
                DefaultValue = c.DefaultValue,
                Placement = c.Placement,
                SettingsJson = c.SettingsJson
            }).ToList(),
            WidgetParameters = await BuildWidgetParameterViewModelsAsync(id),
            ParameterState = parameterState
        });
    }

    [HttpGet("/dashboards/data/{dashboardId}/widgets/{widgetId}")]
    public async Task<IActionResult> WidgetData(int dashboardId, int widgetId)
    {
        if (!await _permissionService.CanViewDashboardAsync(User, dashboardId))
        {
            return Forbid();
        }

        var widget = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Include(w => w.Visualization)
            .ThenInclude(v => v!.Query)
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.DashboardId == dashboardId);

        if (widget?.Visualization?.Query == null)
        {
            return NotFound();
        }

        _logger.LogInformation("Auto-refresh widget {WidgetId} on dashboard {DashboardId} by {UserId}", widgetId, dashboardId, _userManager.GetUserId(User));

        var parameterControls = await _dbContext.DashboardParameterControls
            .AsNoTracking()
            .Where(c => c.DashboardId == dashboardId)
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

        var allowText = User.IsInRole("Admin") || User.IsInRole("Editor");
        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = widget.Visualization.Query.SqlText,
            Definitions = definitions,
            Values = appliedValues,
            AllowText = allowText
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

        var result = await _queryRunner.RunAsync(widget.Visualization.Query.DataSourceId, applyResult.Sql);
        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Refresh failed."
            });
        }

        if (widget.Visualization.Type == VisualizationType.Counter)
        {
            var counterConfig = TryDeserializeCounterConfig(widget.Visualization.ConfigJson) ?? CounterVisualizationConfig.CreateDefault();
            var counterRender = _counterDataBuilder.Build(counterConfig, result.Columns, result.Rows);
            return Ok(new
            {
                success = true,
                columns = result.Columns,
                rows = result.Rows,
                counter = counterRender
            });
        }
        if (widget.Visualization.Type != VisualizationType.Table)
        {
            var chartConfig = TryDeserializeChartConfig(widget.Visualization.ConfigJson) ?? ChartVisualizationConfig.CreateDefault();
            var chartRender = _chartDataBuilder.Build(chartConfig, result.Columns, result.Rows);
            return Ok(new
            {
                success = true,
                columns = result.Columns,
                rows = result.Rows,
                chart = chartRender
            });
        }

        return Ok(new
        {
            success = true,
            columns = result.Columns,
            rows = result.Rows
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SaveParameterControls(int id, DashboardParameterControlsUpdateModel model)
    {
        if (id != model.DashboardId)
        {
            return BadRequest();
        }

        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard == null)
        {
            return NotFound();
        }

        var existing = await _dbContext.DashboardParameterControls
            .Where(c => c.DashboardId == id)
            .ToListAsync();

        var keepIds = new HashSet<int>();
        foreach (var input in model.Controls.Where(c => !string.IsNullOrWhiteSpace(c.Key)))
        {
            var key = input.Key.Trim();
            var title = string.IsNullOrWhiteSpace(input.Title) ? key : input.Title.Trim();
            var existingControl = input.Id.HasValue
                ? existing.FirstOrDefault(c => c.Id == input.Id.Value)
                : existing.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (existingControl == null)
            {
                var control = new DashboardParameterControl
                {
                    DashboardId = id,
                    Key = key,
                    Title = title,
                    Type = input.Type,
                    DefaultValue = input.DefaultValue?.Trim(),
                    Placement = input.Placement,
                    SettingsJson = string.IsNullOrWhiteSpace(input.SettingsJson) ? null : input.SettingsJson.Trim()
                };
                _dbContext.DashboardParameterControls.Add(control);
            }
            else
            {
                existingControl.Key = key;
                existingControl.Title = title;
                existingControl.Type = input.Type;
                existingControl.DefaultValue = input.DefaultValue?.Trim();
                existingControl.Placement = input.Placement;
                existingControl.SettingsJson = string.IsNullOrWhiteSpace(input.SettingsJson) ? null : input.SettingsJson.Trim();
                keepIds.Add(existingControl.Id);
            }
        }

        foreach (var control in existing)
        {
            if (!keepIds.Contains(control.Id) && !model.Controls.Any(c => c.Id == control.Id))
            {
                _dbContext.DashboardParameterControls.Remove(control);
            }
        }

        dashboard.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SaveParameterMappings(int id, DashboardParameterMappingsUpdateModel model)
    {
        if (id != model.DashboardId)
        {
            return BadRequest();
        }

        var widgetIds = model.Mappings.Select(m => m.DashboardWidgetId).Distinct().ToList();
        var widgets = await _dbContext.DashboardWidgets
            .AsNoTracking()
            .Where(w => w.DashboardId == id && widgetIds.Contains(w.Id))
            .ToListAsync();
        if (widgets.Count == 0)
        {
            return NotFound();
        }

        var mappings = await _dbContext.DashboardParameterMappings
            .Where(m => widgetIds.Contains(m.DashboardWidgetId))
            .ToListAsync();

        var queryDefinitions = await _dbContext.QueryParameterDefinitions
            .AsNoTracking()
            .Where(d => model.Mappings.Select(m => m.QueryId).Contains(d.QueryId))
            .ToListAsync();

        foreach (var input in model.Mappings)
        {
            var definition = queryDefinitions.FirstOrDefault(d =>
                d.QueryId == input.QueryId &&
                d.Name.Equals(input.QueryParamKey, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                continue;
            }

            var existing = mappings.FirstOrDefault(m =>
                m.DashboardWidgetId == input.DashboardWidgetId &&
                m.QueryParamKey.Equals(input.QueryParamKey, StringComparison.OrdinalIgnoreCase));

            if (input.BindingType == DashboardParameterBindingType.DashboardControlKey &&
                string.IsNullOrWhiteSpace(input.ControlKey))
            {
                if (existing != null)
                {
                    _dbContext.DashboardParameterMappings.Remove(existing);
                }
                continue;
            }

            var staticValue = input.StaticValue?.Trim();
            if (definition.Type == QueryParameterType.DateRange || definition.Type == QueryParameterType.DateTimeRange)
            {
                if (!string.IsNullOrWhiteSpace(input.StaticValueStart) || !string.IsNullOrWhiteSpace(input.StaticValueEnd))
                {
                    staticValue = JsonSerializer.Serialize(new
                    {
                        start = input.StaticValueStart?.Trim(),
                        end = input.StaticValueEnd?.Trim()
                    });
                }
            }

            if (existing == null)
            {
                _dbContext.DashboardParameterMappings.Add(new DashboardParameterMapping
                {
                    DashboardWidgetId = input.DashboardWidgetId,
                    QueryId = input.QueryId,
                    QueryParamKey = input.QueryParamKey.Trim(),
                    BindingType = input.BindingType,
                    ControlKey = input.BindingType == DashboardParameterBindingType.DashboardControlKey ? input.ControlKey?.Trim() : null,
                    StaticValue = input.BindingType == DashboardParameterBindingType.StaticValue ? staticValue : null
                });
            }
            else
            {
                existing.BindingType = input.BindingType;
                existing.ControlKey = input.BindingType == DashboardParameterBindingType.DashboardControlKey ? input.ControlKey?.Trim() : null;
                existing.StaticValue = input.BindingType == DashboardParameterBindingType.StaticValue ? staticValue : null;
            }
        }

        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddShare(int id, ShareCreateInputModel model)
    {
        if (id != model.EntityId || model.EntityType != ShareEntityType.Dashboard)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(model.UserId) && model.GroupId == null)
        {
            TempData["StatusMessage"] = "Select a user or a group to share.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (!string.IsNullOrWhiteSpace(model.UserId) && model.GroupId != null)
        {
            TempData["StatusMessage"] = "Choose either a user or a group, not both.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var exists = await _dbContext.Shares.AnyAsync(s =>
            s.EntityType == ShareEntityType.Dashboard &&
            s.EntityId == id &&
            ((s.UserId != null && s.UserId == model.UserId) ||
             (s.GroupId != null && s.GroupId == model.GroupId)));

        if (!exists)
        {
            var share = new Share
            {
                EntityType = ShareEntityType.Dashboard,
                EntityId = id,
                UserId = string.IsNullOrWhiteSpace(model.UserId) ? null : model.UserId,
                GroupId = model.GroupId,
                AccessLevel = model.AccessLevel,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Shares.Add(share);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemoveShare(int id, int shareId)
    {
        var share = await _dbContext.Shares.FirstOrDefaultAsync(s => s.Id == shareId && s.EntityType == ShareEntityType.Dashboard && s.EntityId == id);
        if (share == null)
        {
            return NotFound();
        }

        _dbContext.Shares.Remove(share);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("dashboards/{id}/share/enable")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> EnablePublicShare(int id, PublicShareSettingsInputModel model)
    {
        if (!await _dbContext.Dashboards.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var share = await _publicShareService.EnableAsync(PublicShareEntityType.Dashboard, id, userId, model.ExpiresAt);
        return Ok(new PublicShareResponseModel(share));
    }

    [HttpPost("dashboards/{id}/share/disable")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DisablePublicShare(int id)
    {
        if (!await _dbContext.Dashboards.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var share = await _publicShareService.DisableAsync(PublicShareEntityType.Dashboard, id, userId);
        return Ok(new PublicShareResponseModel(share));
    }

    [HttpPost("dashboards/{id}/share/regenerate")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RegeneratePublicShare(int id, PublicShareSettingsInputModel model)
    {
        if (!await _dbContext.Dashboards.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var share = await _publicShareService.RegenerateAsync(PublicShareEntityType.Dashboard, id, userId, model.ExpiresAt);
        return Ok(new PublicShareResponseModel(share));
    }

    [HttpPost("dashboards/{id}/share/set-expiration")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SetPublicShareExpiration(int id, PublicShareSettingsInputModel model)
    {
        if (!await _dbContext.Dashboards.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var share = await _publicShareService.SetExpirationAsync(PublicShareEntityType.Dashboard, id, model.ExpiresAt, userId);
        return Ok(new PublicShareResponseModel(share));
    }

    private async Task<ShareSectionViewModel> BuildShareSectionAsync(ShareEntityType entityType, int entityId)
    {
        var shares = await _dbContext.Shares
            .AsNoTracking()
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .ToListAsync();

        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();

        var userLookup = users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? u.Id) : u.UserName);
        var groupLookup = groups.ToDictionary(g => g.Id, g => g.Name);

        var shareItems = new List<ShareListItemViewModel>();
        foreach (var share in shares)
        {
            var granteeName = "Unknown";
            var granteeType = "User";

            if (share.UserId != null && userLookup.TryGetValue(share.UserId, out var userName))
            {
                granteeName = userName;
                granteeType = "User";
            }
            else if (share.GroupId.HasValue && groupLookup.TryGetValue(share.GroupId.Value, out var groupName))
            {
                granteeName = groupName;
                granteeType = "Group";
            }

            shareItems.Add(new ShareListItemViewModel
            {
                Id = share.Id,
                GranteeName = granteeName,
                GranteeType = granteeType,
                AccessLevel = share.AccessLevel
            });
        }

        return new ShareSectionViewModel
        {
            EntityId = entityId,
            EntityType = entityType,
            Shares = shareItems.OrderBy(s => s.GranteeName).ToList(),
            AvailableUsers = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? u.Id) : u.UserName
            }).ToList(),
            AvailableGroups = groups.Select(g => new SelectListItem
            {
                Value = g.Id.ToString(),
                Text = g.Name
            }).ToList()
        };
    }

    private async Task<IReadOnlyList<DashboardParameterControlViewModel>> BuildDashboardParameterControlsAsync(int dashboardId)
    {
        var controls = await _dbContext.DashboardParameterControls
            .AsNoTracking()
            .Where(c => c.DashboardId == dashboardId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        return controls.Select(c => new DashboardParameterControlViewModel
        {
            Id = c.Id,
            Key = c.Key,
            Title = c.Title,
            Type = c.Type,
            DefaultValue = c.DefaultValue,
            Placement = c.Placement,
            SettingsJson = c.SettingsJson
        }).ToList();
    }

    private async Task<IReadOnlyList<DashboardWidgetParameterViewModel>> BuildWidgetParameterViewModelsAsync(int dashboardId)
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

    private static ChartVisualizationConfig? TryDeserializeChartConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ChartVisualizationConfig>(json, ChartConfigJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CounterVisualizationConfig? TryDeserializeCounterConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CounterVisualizationConfig>(json, CounterConfigJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
