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

    public DashboardsController(
        ApplicationDbContext dbContext,
        QueryRunner queryRunner,
        PermissionService permissionService,
        UserManager<IdentityUser> userManager,
        PublicShareService publicShareService)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
        _permissionService = permissionService;
        _userManager = userManager;
        _publicShareService = publicShareService;
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
            ShareSection = await BuildShareSectionAsync(ShareEntityType.Dashboard, dashboard.Id)
        };

        return View(model);
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

        var share = await _publicShareService.GetActiveShareAsync(PublicShareEntityType.Dashboard, id);
        return View(new DashboardViewModel
        {
            Dashboard = dashboard,
            Widgets = viewWidgets,
            PublicShareToken = share?.Token,
            PublicShareEnabled = share?.IsEnabled ?? false,
            PublicShareExpiresAt = share?.ExpiresAt
        });
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
}
