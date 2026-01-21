using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Controllers;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Controllers;

public class PublicControllerTests
{
    [Fact]
    public async Task Dashboard_Returns_View_When_Token_Is_Valid()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");

        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Primary",
            ConnectionStringEncrypted = protector.Protect("Server=localhost\\INVALID;Database=master;Trusted_Connection=True;Connection Timeout=1;"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var query = new Query
        {
            Name = "Query",
            DataSourceId = 1,
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
            Type = VisualizationType.Line,
            ConfigJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Visualizations.Add(visualization);
        await dbContext.SaveChangesAsync();

        var dashboard = new Dashboard
        {
            Name = "Public Dashboard",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Dashboards.Add(dashboard);
        await dbContext.SaveChangesAsync();

        dbContext.DashboardWidgets.Add(new DashboardWidget
        {
            DashboardId = dashboard.Id,
            VisualizationId = visualization.Id,
            X = 0,
            Y = 0,
            Width = 4,
            Height = 4
        });
        dbContext.PublicShares.Add(new PublicShare
        {
            EntityType = PublicShareEntityType.Dashboard,
            EntityId = dashboard.Id,
            Token = "valid-token",
            IsEnabled = true,
            AccessLevel = ShareAccessLevel.View,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "owner"
        });
        await dbContext.SaveChangesAsync();

        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var publicShareService = new PublicShareService(dbContext);
        var controller = new PublicController(dbContext, queryRunner, publicShareService);

        var result = await controller.Dashboard("valid-token");

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Dashboard_Returns_NotFound_When_Token_Is_Invalid()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var publicShareService = new PublicShareService(dbContext);
        var controller = new PublicController(dbContext, queryRunner, publicShareService);

        var result = await controller.Dashboard("missing-token");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task EmbedDashboard_Returns_NotFound_When_Token_Is_Disabled()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.PublicShares.Add(new PublicShare
        {
            EntityType = PublicShareEntityType.Dashboard,
            EntityId = 99,
            Token = "disabled-token",
            IsEnabled = false,
            AccessLevel = ShareAccessLevel.View,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "owner"
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var publicShareService = new PublicShareService(dbContext);
        var controller = new PublicController(dbContext, queryRunner, publicShareService);

        var result = await controller.EmbedDashboard("disabled-token");

        result.Should().BeOfType<NotFoundResult>();
    }
}
