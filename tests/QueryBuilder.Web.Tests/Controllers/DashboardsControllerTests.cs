using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Controllers;
using QueryBuilder.Web.Models.Dashboards;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Controllers;

public class DashboardsControllerTests
{
    [Fact]
    public async Task Index_Returns_Empty_List_When_No_Access()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Dashboards.Add(new Dashboard
        {
            Name = "Ops",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("viewer-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var publicShareService = new PublicShareService(dbContext);

        var parameterService = new FakeQueryParameterService();
        var controller = new DashboardsController(
            dbContext,
            queryRunner,
            permissionService,
            userManager.Object,
            publicShareService,
            NullLogger<DashboardsController>.Instance,
            parameterService,
            new TableVisualizationConfigBuilder(),
            new ChartVisualizationDataBuilder(),
            new CounterVisualizationDataBuilder())
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("viewer-1", "Viewer"))
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().BeAssignableTo<List<DashboardListItemViewModel>>();
        ((List<DashboardListItemViewModel>)viewResult.Model!).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveLayout_Updates_And_Removes_Widgets()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dashboard = new Dashboard
        {
            Name = "Main",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Dashboards.Add(dashboard);
        await dbContext.SaveChangesAsync();

        var existing = new DashboardWidget
        {
            DashboardId = dashboard.Id,
            VisualizationId = 10,
            X = 0,
            Y = 0,
            Width = 4,
            Height = 3
        };
        var removable = new DashboardWidget
        {
            DashboardId = dashboard.Id,
            VisualizationId = 11,
            X = 1,
            Y = 1,
            Width = 2,
            Height = 2
        };
        dbContext.DashboardWidgets.AddRange(existing, removable);
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var publicShareService = new PublicShareService(dbContext);

        var parameterService = new FakeQueryParameterService();
        var controller = new DashboardsController(
            dbContext,
            queryRunner,
            permissionService,
            userManager.Object,
            publicShareService,
            NullLogger<DashboardsController>.Instance,
            parameterService,
            new TableVisualizationConfigBuilder(),
            new ChartVisualizationDataBuilder(),
            new CounterVisualizationDataBuilder())
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("editor-1", "Editor"))
        };

        var request = new DashboardSaveLayoutRequest
        {
            Widgets = new List<DashboardWidgetSaveModel>
            {
                new()
                {
                    Id = existing.Id,
                    VisualizationId = existing.VisualizationId,
                    X = 2,
                    Y = 2,
                    Width = 6,
                    Height = 4
                },
                new()
                {
                    VisualizationId = 12,
                    X = 3,
                    Y = 3,
                    Width = 5,
                    Height = 5
                }
            }
        };

        var result = await controller.SaveLayout(dashboard.Id, request);

        result.Should().BeOfType<OkResult>();
        dbContext.DashboardWidgets.Should().HaveCount(2);
        dbContext.DashboardWidgets.Any(w => w.VisualizationId == 11).Should().BeFalse();
        var updated = dbContext.DashboardWidgets.Single(w => w.VisualizationId == 10);
        updated.X.Should().Be(2);
        updated.Width.Should().Be(6);
    }

    [Fact]
    public async Task View_Returns_Forbid_When_User_Lacks_Permission()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Dashboards.Add(new Dashboard
        {
            Id = 1,
            Name = "Main",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("viewer-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var publicShareService = new PublicShareService(dbContext);

        var parameterService = new FakeQueryParameterService();
        var controller = new DashboardsController(
            dbContext,
            queryRunner,
            permissionService,
            userManager.Object,
            publicShareService,
            NullLogger<DashboardsController>.Instance,
            parameterService,
            new TableVisualizationConfigBuilder(),
            new ChartVisualizationDataBuilder(),
            new CounterVisualizationDataBuilder())
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("viewer-1", "Viewer"))
        };

        var result = await controller.View(1);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task View_Returns_Dashboard_With_Widgets_When_Shared()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "viewer-2";
        var dashboard = new Dashboard
        {
            Name = "Main",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Dashboards.Add(dashboard);
        await dbContext.SaveChangesAsync();

        var query = new Query
        {
            Name = "Query",
            DataSourceId = 999,
            SqlText = "select 1",
            CreatedById = "owner",
            UpdatedById = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Queries.Add(query);
        await dbContext.SaveChangesAsync();

        var visualization = new Visualization
        {
            QueryId = query.Id,
            Name = "Chart",
            Type = VisualizationType.Chart,
            ConfigJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Visualizations.Add(visualization);
        await dbContext.SaveChangesAsync();

        dbContext.DashboardWidgets.Add(new DashboardWidget
        {
            DashboardId = dashboard.Id,
            VisualizationId = visualization.Id,
            X = 0,
            Y = 0,
            Width = 6,
            Height = 4
        });
        dbContext.Shares.Add(new Share
        {
            EntityType = ShareEntityType.Dashboard,
            EntityId = dashboard.Id,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create(userId);
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var publicShareService = new PublicShareService(dbContext);

        var parameterService = new FakeQueryParameterService();
        var controller = new DashboardsController(
            dbContext,
            queryRunner,
            permissionService,
            userManager.Object,
            publicShareService,
            NullLogger<DashboardsController>.Instance,
            parameterService,
            new TableVisualizationConfigBuilder(),
            new ChartVisualizationDataBuilder(),
            new CounterVisualizationDataBuilder())
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser(userId, "Viewer"))
        };

        var result = await controller.View(dashboard.Id);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().NotBeNull();
    }
}
