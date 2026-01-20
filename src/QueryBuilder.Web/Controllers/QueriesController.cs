using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

    public QueriesController(
        ApplicationDbContext dbContext,
        UserManager<IdentityUser> userManager,
        QueryRunner queryRunner,
        PermissionService permissionService,
        SchemaBrowserService schemaBrowserService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _queryRunner = queryRunner;
        _permissionService = permissionService;
        _schemaBrowserService = schemaBrowserService;
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
        return View(new QueryEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create(QueryEditViewModel model)
    {
        await LoadDataSourcesAsync();

        if (string.Equals(model.SubmitAction, "run", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Remove(nameof(model.Name));
            ModelState.Remove(nameof(model.Description));
            if (!await ValidateQueryAsync(model))
            {
                return View(model);
            }

            model.Result = await RunQueryAsync(model.DataSourceId!.Value, model.SqlText, null);
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

        if (string.Equals(model.SubmitAction, "run", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Remove(nameof(model.Name));
            ModelState.Remove(nameof(model.Description));
            if (!await ValidateQueryAsync(model))
            {
                return View(model);
            }

            model.Result = await RunQueryAsync(model.DataSourceId!.Value, model.SqlText, query.Id);
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

    private async Task LoadDataSourcesAsync()
    {
        var dataSources = await _dbContext.DataSources
            .AsNoTracking()
            .Where(ds => ds.IsActive)
            .OrderBy(ds => ds.Name)
            .ToListAsync();

        ViewData["DataSources"] = new SelectList(dataSources, nameof(DataSource.Id), nameof(DataSource.Name));
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

    private async Task<QueryResultViewModel> RunQueryAsync(int dataSourceId, string sqlText, int? queryId)
    {
        var result = await _queryRunner.RunAsync(dataSourceId, sqlText);
        var execution = new QueryExecution
        {
            QueryId = queryId ?? 0,
            StartedAt = DateTimeOffset.UtcNow,
            Status = result.Success ? QueryExecutionStatus.Success : QueryExecutionStatus.Failed,
            DurationMs = result.DurationMs,
            RowCount = result.RowCount,
            ErrorMessage = result.Success ? null : result.ErrorMessage
        };

        if (queryId.HasValue)
        {
            _dbContext.QueryExecutions.Add(execution);
            await _dbContext.SaveChangesAsync();
        }

        return result;
    }
}
