using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Admin;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IFeatureFlagService _featureFlags;

    public AdminController(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, IFeatureFlagService featureFlags)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _featureFlags = featureFlags;
    }

    public IActionResult Features()
    {
        return RedirectToAction(nameof(FeatureFlags));
    }

    public async Task<IActionResult> FeatureFlags()
    {
        var flags = await _featureFlags.GetAllAsync();
        return View(new FeatureFlagsViewModel { Flags = flags });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFeature(string key, bool isEnabled, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            TempData["StatusMessage"] = "Feature key is required.";
            return RedirectToAction(nameof(FeatureFlags));
        }

        var updatedBy = User?.Identity?.Name ?? "unknown";
        try
        {
            var rowVersionBytes = string.IsNullOrWhiteSpace(rowVersion) ? null : Convert.FromBase64String(rowVersion);
            await _featureFlags.UpdateAsync(key, isEnabled, updatedBy, rowVersionBytes);
            TempData["StatusMessage"] = "Feature flag updated.";
        }
        catch (FeatureFlagConcurrencyException ex)
        {
            TempData["StatusMessage"] = ex.Message;
        }
        catch (FormatException)
        {
            TempData["StatusMessage"] = "Unable to update feature flag due to invalid concurrency token.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["StatusMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(FeatureFlags));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFeature(FeatureFlagCreateInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.Key))
        {
            TempData["StatusMessage"] = "Feature key is required.";
            return RedirectToAction(nameof(FeatureFlags));
        }

        var updatedBy = User?.Identity?.Name ?? "unknown";
        try
        {
            await _featureFlags.CreateAsync(input.Key, input.Description, input.IsEnabled, updatedBy);
            TempData["StatusMessage"] = "Feature flag created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["StatusMessage"] = ex.Message;
        }
        catch (ArgumentException ex)
        {
            TempData["StatusMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(FeatureFlags));
    }

    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var roles = new List<string> { "Admin", "Editor", "Viewer" };
        var items = new List<UserItemViewModel>();
        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            items.Add(new UserItemViewModel
            {
                Id = user.Id,
                UserName = string.IsNullOrWhiteSpace(user.UserName) ? user.Id : user.UserName,
                Email = user.Email ?? string.Empty,
                Role = userRoles.FirstOrDefault() ?? "Viewer"
            });
        }

        return View(new UsersViewModel
        {
            Users = items,
            Roles = roles
        });
    }

    public async Task<IActionResult> Groups()
    {
        var groups = await _dbContext.Groups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();

        var members = await _dbContext.GroupMembers
            .AsNoTracking()
            .ToListAsync();

        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var userLookup = users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? u.Id) : u.UserName);

        var model = new GroupsViewModel
        {
            Groups = groups.Select(g => new GroupItemViewModel
            {
                Id = g.Id,
                Name = g.Name,
                Members = members
                    .Where(m => m.GroupId == g.Id)
                    .Select(m => new GroupMemberViewModel
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        UserName = userLookup.TryGetValue(m.UserId, out var name) ? name : m.UserId
                    })
                    .OrderBy(m => m.UserName)
                    .ToList()
            }).ToList(),
            Users = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? u.Id) : u.UserName
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(GroupsViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NewGroupName))
        {
            TempData["StatusMessage"] = "Group name is required.";
            return RedirectToAction(nameof(Groups));
        }

        var name = model.NewGroupName.Trim();
        var exists = await _dbContext.Groups.AnyAsync(g => g.Name == name);
        if (exists)
        {
            TempData["StatusMessage"] = "A group with that name already exists.";
            return RedirectToAction(nameof(Groups));
        }

        var group = new Group
        {
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Group created.";
        return RedirectToAction(nameof(Groups));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGroupMember(int groupId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction(nameof(Groups));
        }

        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound();
        }

        var exists = await _dbContext.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (!exists)
        {
            _dbContext.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Groups));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGroupMember(int memberId)
    {
        var member = await _dbContext.GroupMembers.FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null)
        {
            return NotFound();
        }

        _dbContext.GroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Groups));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
        {
            return NotFound();
        }

        var members = await _dbContext.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        _dbContext.GroupMembers.RemoveRange(members);
        _dbContext.Groups.Remove(group);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Groups));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var validRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Editor", "Viewer" };
        if (!validRoles.Contains(role))
        {
            TempData["StatusMessage"] = "Invalid role.";
            return RedirectToAction(nameof(Users));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await _userManager.AddToRoleAsync(user, role);
        TempData["StatusMessage"] = "User role updated.";
        return RedirectToAction(nameof(Users));
    }
}
