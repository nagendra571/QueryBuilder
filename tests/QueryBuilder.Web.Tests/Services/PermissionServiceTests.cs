using FluentAssertions;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class PermissionServiceTests
{
    [Fact]
    public async Task CanViewQueryAsync_Returns_True_For_Admin()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "admin-1";
        var userManager = UserManagerMockHelper.Create(userId);
        var service = new PermissionService(dbContext, userManager.Object);
        var principal = ControllerTestHelpers.CreateUser(userId, "Admin");

        var result = await service.CanViewQueryAsync(principal, 123);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewQueryAsync_Returns_True_When_User_Share_Exists()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "user-1";
        dbContext.Shares.Add(new Share
        {
            EntityType = ShareEntityType.Query,
            EntityId = 42,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create(userId);
        var service = new PermissionService(dbContext, userManager.Object);
        var principal = ControllerTestHelpers.CreateUser(userId, "Viewer");

        var result = await service.CanViewQueryAsync(principal, 42);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewDashboardAsync_Returns_True_When_Group_Share_Exists()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "user-2";
        var group = new Group { Name = "Team A" };
        dbContext.Groups.Add(group);
        await dbContext.SaveChangesAsync();

        dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = userId
        });
        dbContext.Shares.Add(new Share
        {
            EntityType = ShareEntityType.Dashboard,
            EntityId = 7,
            GroupId = group.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create(userId);
        var service = new PermissionService(dbContext, userManager.Object);
        var principal = ControllerTestHelpers.CreateUser(userId, "Viewer");

        var result = await service.CanViewDashboardAsync(principal, 7);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessibleQueryIdsAsync_Returns_Only_Shared_Queries_For_Viewer()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "viewer-1";
        dbContext.Shares.AddRange(
            new Share
            {
                EntityType = ShareEntityType.Query,
                EntityId = 10,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Share
            {
                EntityType = ShareEntityType.Query,
                EntityId = 20,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create(userId);
        var service = new PermissionService(dbContext, userManager.Object);
        var principal = ControllerTestHelpers.CreateUser(userId, "Viewer");

        var result = await service.GetAccessibleQueryIdsAsync(principal);

        result.Should().BeEquivalentTo(new[] { 10, 20 });
    }

    [Fact]
    public async Task GetAccessibleDashboardIdsAsync_Returns_All_For_Editor()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Dashboards.AddRange(
            new Dashboard { Name = "Ops", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Dashboard { Name = "Finance", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync();

        var userId = "editor-1";
        var userManager = UserManagerMockHelper.Create(userId);
        var service = new PermissionService(dbContext, userManager.Object);
        var principal = ControllerTestHelpers.CreateUser(userId, "Editor");

        var result = await service.GetAccessibleDashboardIdsAsync(principal);

        result.Should().HaveCount(2);
    }
}
