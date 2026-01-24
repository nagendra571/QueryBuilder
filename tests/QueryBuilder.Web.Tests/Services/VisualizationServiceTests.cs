using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class VisualizationServiceTests
{
    [Fact]
    public async Task DeleteVisualizationAsync_SoftDeletes_When_User_Is_Owner_Editor()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor-1",
            UpdatedById = "editor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Visualizations.Add(new Visualization
        {
            Id = 10,
            QueryId = 1,
            Name = "Viz",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var service = new VisualizationService(dbContext, permissionService, userManager.Object);
        var user = ControllerTestHelpers.CreateUser("editor-1", "Editor");

        var result = await service.DeleteVisualizationAsync(10, user);

        result.Success.Should().BeTrue();
        var visualization = dbContext.Visualizations.IgnoreQueryFilters().Single(v => v.Id == 10);
        visualization.IsDeleted.Should().BeTrue();
        visualization.DeletedAt.Should().NotBeNull();
        visualization.DeletedByUserId.Should().Be("editor-1");
    }

    [Fact]
    public async Task DeleteVisualizationAsync_Blocks_Viewers()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor-1",
            UpdatedById = "editor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Visualizations.Add(new Visualization
        {
            Id = 10,
            QueryId = 1,
            Name = "Viz",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create("viewer-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var service = new VisualizationService(dbContext, permissionService, userManager.Object);
        var user = ControllerTestHelpers.CreateUser("viewer-1", "Viewer");

        var result = await service.DeleteVisualizationAsync(10, user);

        result.Success.Should().BeFalse();
        result.IsForbidden.Should().BeTrue();
        dbContext.Visualizations.IgnoreQueryFilters().Single(v => v.Id == 10).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVisualizationAsync_Blocks_When_Referenced_By_Dashboard()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor-1",
            UpdatedById = "editor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Visualizations.Add(new Visualization
        {
            Id = 10,
            QueryId = 1,
            Name = "Viz",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Dashboards.Add(new Dashboard
        {
            Id = 5,
            Name = "Sales",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.DashboardWidgets.Add(new DashboardWidget
        {
            DashboardId = 5,
            VisualizationId = 10
        });
        await dbContext.SaveChangesAsync();

        var userManager = UserManagerMockHelper.Create("admin-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var service = new VisualizationService(dbContext, permissionService, userManager.Object);
        var user = ControllerTestHelpers.CreateUser("admin-1", "Admin");

        var result = await service.DeleteVisualizationAsync(10, user);

        result.Success.Should().BeFalse();
        result.AffectedDashboards.Should().Contain("Sales");
        dbContext.Visualizations.IgnoreQueryFilters().Single(v => v.Id == 10).IsDeleted.Should().BeFalse();
    }
}
