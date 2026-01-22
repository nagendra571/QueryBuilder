using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Sharing;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[Authorize]
public class QueriesController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly QueryRunner _queryRunner;
    private readonly PermissionService _permissionService;
    private readonly SchemaBrowserService _schemaBrowserService;
    private readonly IQueryParameterService _parameterService;

    public QueriesController(
        ApplicationDbContext dbContext,
        UserManager<IdentityUser> userManager,
        QueryRunner queryRunner,
        PermissionService permissionService,
        SchemaBrowserService schemaBrowserService,
        IQueryParameterService parameterService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _queryRunner = queryRunner;
        _permissionService = permissionService;
        _schemaBrowserService = schemaBrowserService;
        _parameterService = parameterService;
    }

    public async Task<IActionResult> Index()
    {
        IQueryable<Query> queryable = _dbContext.Queries
            .AsNoTracking()
            .Include(q => q.DataSource);

        if (!User.IsInRole("Admin") && !User.IsInRole("Editor"))
        {
            var allowedIds = await _permissionService.GetAccessibleQueryIdsAsync(User);
            if (allowedIds.Count == 0)
            {
                return View(new List<QueryListItemViewModel>());
            }

            queryable = queryable.Where(q => allowedIds.Contains(q.Id));
        }

        var queries = await queryable
            .OrderByDescending(q => q.UpdatedAt)
            .Select(q => new QueryListItemViewModel
            {
                Id = q.Id,
                Name = q.Name,
                DataSourceName = q.DataSource != null ? q.DataSource.Name : "Unknown",
                UpdatedAt = q.UpdatedAt
            })
            .ToListAsync();

        return View(queries);
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create()
    {
        await LoadDataSourcesAsync();
        await LoadParameterQueriesAsync();
        return View(new QueryEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create(QueryEditViewModel model)
    {
        await LoadDataSourcesAsync();
        await LoadParameterQueriesAsync();
        model.ParameterValues ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(model.SubmitAction, "run", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Remove(nameof(model.Name));
            ModelState.Remove(nameof(model.Description));
            if (!await ValidateQueryAsync(model))
            {
                return View(model);
            }

            model.Result = await RunQueryAsync(model.DataSourceId!.Value, model.SqlText, null, model.ParameterValues);
            model.ParameterDefinitions = Array.Empty<QueryParameterDefinitionViewModel>();
            model.ParsedTokens = QueryParameterParser.ExtractTokens(model.SqlText);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var query = new Query
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            DataSourceId = model.DataSourceId!.Value,
            SqlText = model.SqlText,
            CreatedById = userId,
            UpdatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Queries.Add(query);
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Query created.";
        return RedirectToAction(nameof(Edit), new { id = query.Id });
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id)
    {
        var query = await _dbContext.Queries.FirstOrDefaultAsync(q => q.Id == id);
        if (query == null)
        {
            return NotFound();
        }

        await LoadDataSourcesAsync();
        await LoadParameterQueriesAsync(query.Id);
        var visualizations = await _dbContext.Visualizations
            .AsNoTracking()
            .Where(v => v.QueryId == query.Id)
            .OrderBy(v => v.Name)
            .Select(v => new QueryVisualizationListItemViewModel
            {
                Id = v.Id,
                Name = v.Name,
                Type = v.Type.ToString()
            })
            .ToListAsync();
        var model = new QueryEditViewModel
        {
            Id = query.Id,
            Name = query.Name,
            Description = query.Description,
            DataSourceId = query.DataSourceId,
            SqlText = query.SqlText,
            Visualizations = visualizations,
            ShareSection = await BuildShareSectionAsync(ShareEntityType.Query, query.Id)
        };

        await PopulateParameterModelAsync(model, query.Id);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id, QueryEditViewModel model)
    {
        var query = await _dbContext.Queries.FirstOrDefaultAsync(q => q.Id == id);
        if (query == null)
        {
            return NotFound();
        }

        await LoadDataSourcesAsync();
        await LoadParameterQueriesAsync(query.Id);
        model.ParameterValues ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await PopulateParameterModelAsync(model, query.Id);

        if (string.Equals(model.SubmitAction, "run", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Remove(nameof(model.Name));
            ModelState.Remove(nameof(model.Description));
            if (!await ValidateQueryAsync(model))
            {
                return View(model);
            }

            model.Result = await RunQueryAsync(model.DataSourceId!.Value, model.SqlText, query.Id, model.ParameterValues);
            model.Visualizations = await _dbContext.Visualizations
                .AsNoTracking()
                .Where(v => v.QueryId == query.Id)
                .OrderBy(v => v.Name)
                .Select(v => new QueryVisualizationListItemViewModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    Type = v.Type.ToString()
                })
                .ToListAsync();
            model.ShareSection = await BuildShareSectionAsync(ShareEntityType.Query, query.Id);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.Visualizations = await _dbContext.Visualizations
                .AsNoTracking()
                .Where(v => v.QueryId == query.Id)
                .OrderBy(v => v.Name)
                .Select(v => new QueryVisualizationListItemViewModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    Type = v.Type.ToString()
                })
                .ToListAsync();
            model.ShareSection = await BuildShareSectionAsync(ShareEntityType.Query, query.Id);
            return View(model);
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        query.Name = model.Name.Trim();
        query.Description = model.Description?.Trim();
        query.DataSourceId = model.DataSourceId!.Value;
        query.SqlText = model.SqlText;
        query.UpdatedById = userId;
        query.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Query updated.";
        return RedirectToAction(nameof(Edit), new { id = query.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.CanViewQueryAsync(User, id))
        {
            return Forbid();
        }

        var query = await _dbContext.Queries
            .AsNoTracking()
            .Include(q => q.DataSource)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (query == null)
        {
            return NotFound();
        }

        var visualizations = await _dbContext.Visualizations
            .AsNoTracking()
            .Where(v => v.QueryId == query.Id)
            .OrderBy(v => v.Name)
            .Select(v => new QueryVisualizationListItemViewModel
            {
                Id = v.Id,
                Name = v.Name,
                Type = v.Type.ToString()
            })
            .ToListAsync();

        var model = new QueryDetailsViewModel
        {
            Id = query.Id,
            Name = query.Name,
            Description = query.Description,
            SqlText = query.SqlText,
            DataSourceName = query.DataSource?.Name ?? "Unknown",
            UpdatedAt = query.UpdatedAt,
            Visualizations = visualizations
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Schema(int dataSourceId)
    {
        if (dataSourceId <= 0)
        {
            return BadRequest();
        }

        var result = await _schemaBrowserService.GetTablesAsync(dataSourceId);
        return Json(result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SchemaColumns(int dataSourceId, string schemaName, string tableName)
    {
        if (dataSourceId <= 0 || string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(tableName))
        {
            return BadRequest();
        }

        var result = await _schemaBrowserService.GetColumnsAsync(dataSourceId, schemaName, tableName);
        return Json(result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> ParameterQueryPreview(int queryId)
    {
        if (queryId <= 0)
        {
            return BadRequest();
        }

        if (!await _permissionService.CanViewQueryAsync(User, queryId))
        {
            return Forbid();
        }

        var query = await _dbContext.Queries
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == queryId);
        if (query == null)
        {
            return NotFound();
        }

        var result = await _queryRunner.RunAsync(query.DataSourceId, query.SqlText);
        if (!result.Success)
        {
            return Ok(new { success = false, errorMessage = result.ErrorMessage ?? "Failed to load values." });
        }

        var options = result.Rows
            .Take(25)
            .Select(row => new
            {
                label = row.Count > 0 ? row[0] : string.Empty,
                value = row.Count > 1 ? row[1] : (row.Count > 0 ? row[0] : string.Empty)
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.label))
            .ToList();

        return Ok(new { success = true, options });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddShare(int id, ShareCreateInputModel model)
    {
        if (id != model.EntityId || model.EntityType != ShareEntityType.Query)
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
            s.EntityType == ShareEntityType.Query &&
            s.EntityId == id &&
            ((s.UserId != null && s.UserId == model.UserId) ||
             (s.GroupId != null && s.GroupId == model.GroupId)));

        if (!exists)
        {
            var share = new Share
            {
                EntityType = ShareEntityType.Query,
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
        var share = await _dbContext.Shares.FirstOrDefaultAsync(s => s.Id == shareId && s.EntityType == ShareEntityType.Query && s.EntityId == id);
        if (share == null)
        {
            return NotFound();
        }

        _dbContext.Shares.Remove(share);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddParameterDefinition(int id, QueryParameterDefinitionInputModel model)
    {
        var query = await _dbContext.Queries.FirstOrDefaultAsync(q => q.Id == id);
        if (query == null)
        {
            return NotFound();
        }

        var name = model.Name.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-zA-Z0-9_]+$"))
        {
            TempData["StatusMessage"] = "Parameter name must be alphanumeric or underscore.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var definition = await _dbContext.QueryParameterDefinitions
            .FirstOrDefaultAsync(p => p.QueryId == id && p.Name == name);

        if (definition == null)
        {
            definition = new QueryParameterDefinition
            {
                QueryId = id,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.QueryParameterDefinitions.Add(definition);
        }

        definition.Title = string.IsNullOrWhiteSpace(model.Title) ? name : model.Title.Trim();
        definition.Type = model.Type;
        definition.DefaultValue = model.DefaultValue;
        definition.SettingsJson = string.IsNullOrWhiteSpace(model.SettingsJson) ? "{}" : model.SettingsJson;
        definition.IsRequired = model.IsRequired;
        definition.UpdatedAt = DateTimeOffset.UtcNow;

        if (!query.SqlText.Contains($"{{{{{name}}}}}", StringComparison.OrdinalIgnoreCase))
        {
            if (model.Type == QueryParameterType.DateRange || model.Type == QueryParameterType.DateTimeRange)
            {
                query.SqlText = $"{EnsureWhereClause(query.SqlText)}\n-- {name} range\n-- AND [YourDateColumn] >= {{%{name}.start%}}\n-- AND [YourDateColumn] <= {{%{name}.end%}}"
                    .Replace("{%", "{{")
                    .Replace("%}", "}}");
            }
            else
            {
                query.SqlText = $"{EnsureWhereClause(query.SqlText)}\n-- parameter {name}\n-- AND [YourColumn] = {{{{{name}}}}}";
            }
        }

        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Parameter saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemoveParameterDefinition(int id, int parameterId)
    {
        var definition = await _dbContext.QueryParameterDefinitions
            .FirstOrDefaultAsync(p => p.Id == parameterId && p.QueryId == id);
        if (definition == null)
        {
            return NotFound();
        }

        _dbContext.QueryParameterDefinitions.Remove(definition);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Parameter removed.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task LoadDataSourcesAsync()
    {
        var dataSources = await _dbContext.DataSources
            .AsNoTracking()
            .Where(ds => ds.IsActive)
            .OrderBy(ds => ds.Name)
            .ToListAsync();

        ViewData["DataSources"] = new SelectList(dataSources, nameof(DataSource.Id), nameof(DataSource.Name));
    }

    private async Task LoadParameterQueriesAsync(int? excludeQueryId = null)
    {
        IQueryable<Query> queryable = _dbContext.Queries.AsNoTracking();

        if (!User.IsInRole("Admin") && !User.IsInRole("Editor"))
        {
            var allowedIds = await _permissionService.GetAccessibleQueryIdsAsync(User);
            if (allowedIds.Count == 0)
            {
                ViewBag.ParameterQueries = new List<SelectListItem>();
                return;
            }

            queryable = queryable.Where(q => allowedIds.Contains(q.Id));
        }

        if (excludeQueryId.HasValue)
        {
            queryable = queryable.Where(q => q.Id != excludeQueryId.Value);
        }

        var queries = await queryable
            .OrderBy(q => q.Name)
            .Select(q => new SelectListItem
            {
                Value = q.Id.ToString(),
                Text = q.Name
            })
            .ToListAsync();

        ViewBag.ParameterQueries = queries;
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

    private async Task<bool> ValidateQueryAsync(QueryEditViewModel model)
    {
        if (model.DataSourceId == null)
        {
            ModelState.AddModelError(nameof(model.DataSourceId), "Data source is required.");
        }

        if (string.IsNullOrWhiteSpace(model.SqlText))
        {
            ModelState.AddModelError(nameof(model.SqlText), "SQL is required.");
        }

        var trimmed = model.SqlText.TrimStart();
        if (!string.IsNullOrEmpty(trimmed) &&
            !trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.SqlText), "Only SELECT queries are allowed in the MVP.");
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        var dataSourceExists = await _dbContext.DataSources.AnyAsync(ds => ds.Id == model.DataSourceId && ds.IsActive);
        if (!dataSourceExists)
        {
            ModelState.AddModelError(nameof(model.DataSourceId), "Selected data source is not available.");
            return false;
        }

        return true;
    }

    private static string EnsureWhereClause(string sql)
    {
        var trimmed = sql.TrimEnd();
        if (trimmed.IndexOf("where", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return trimmed;
        }

        return $"{trimmed}\nWHERE 1=1";
    }

    private async Task PopulateParameterModelAsync(QueryEditViewModel model, int queryId)
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
                SettingsJson = definition.SettingsJson,
                Options = await BuildOptionsAsync(definition)
            });
        }

        model.ParameterDefinitions = definitionModels;
        model.ParsedTokens = QueryParameterParser.ExtractTokens(model.SqlText);

        var tokenInfo = model.ParsedTokens
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

        foreach (var definition in definitionModels)
        {
            if (!model.ParameterValues.ContainsKey(definition.Name))
            {
                model.ParameterValues[definition.Name] = definition.DefaultValue;
            }

            if (definition.Type == QueryParameterType.DateRange || definition.Type == QueryParameterType.DateTimeRange)
            {
                var startKey = $"{definition.Name}.start";
                var endKey = $"{definition.Name}.end";
                if (!model.ParameterValues.ContainsKey(startKey))
                {
                    model.ParameterValues[startKey] = null;
                }
                if (!model.ParameterValues.ContainsKey(endKey))
                {
                    model.ParameterValues[endKey] = null;
                }
            }
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
                var label = option.TryGetProperty("label", out var labelElement)
                    ? labelElement.GetString() ?? string.Empty
                    : option.TryGetProperty("value", out var valueElement)
                        ? valueElement.GetString() ?? string.Empty
                        : string.Empty;
                var value = option.TryGetProperty("value", out var valueElement2)
                    ? valueElement2.GetString() ?? string.Empty
                    : label;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    options.Add(new QueryParameterOptionViewModel
                    {
                        Label = string.IsNullOrWhiteSpace(label) ? value : label,
                        Value = value
                    });
                }
            }
        }
        else if (document.RootElement.TryGetProperty("sourceQueryId", out var queryIdElement) &&
                 queryIdElement.TryGetInt32(out var sourceQueryId))
        {
            var query = await _dbContext.Queries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == sourceQueryId);
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
                if (row.Count == 0) continue;
                var label = row[0] ?? string.Empty;
                var value = row.Count > 1 ? row[1] ?? label : label;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    options.Add(new QueryParameterOptionViewModel
                    {
                        Label = string.IsNullOrWhiteSpace(label) ? value : label,
                        Value = value
                    });
                }
            }
        }

        return options;
    }

    private async Task<QueryResultViewModel> RunQueryAsync(int dataSourceId, string sqlText, int? queryId, Dictionary<string, string?>? parameterValues)
    {
        var definitions = queryId.HasValue
            ? await _dbContext.QueryParameterDefinitions
                .AsNoTracking()
                .Where(p => p.QueryId == queryId.Value)
                .ToListAsync()
            : new List<QueryParameterDefinition>();

        var allowText = User.IsInRole("Admin") || User.IsInRole("Editor");
        var applyResult = await _parameterService.ApplyAsync(new QueryParameterApplyRequest
        {
            Sql = sqlText,
            Definitions = definitions,
            Values = parameterValues ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            AllowText = allowText
        });

        if (!applyResult.Success)
        {
            return new QueryResultViewModel
            {
                Success = false,
                ErrorMessage = string.Join(" ", applyResult.Errors)
            };
        }

        var result = await _queryRunner.RunAsync(dataSourceId, applyResult.Sql);
        var execution = new QueryExecution
        {
            QueryId = queryId ?? 0,
            StartedAt = DateTimeOffset.UtcNow,
            Status = result.Success ? QueryExecutionStatus.Success : QueryExecutionStatus.Failed,
            DurationMs = result.DurationMs,
            RowCount = result.RowCount,
            ErrorMessage = result.Success ? null : result.ErrorMessage,
            ParametersJson = applyResult.AppliedValues.Count > 0 ? JsonSerializer.Serialize(applyResult.AppliedValues) : null
        };

        if (queryId.HasValue)
        {
            _dbContext.QueryExecutions.Add(execution);
            await _dbContext.SaveChangesAsync();
        }

        return result;
    }
}
