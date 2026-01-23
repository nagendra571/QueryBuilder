using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Controllers;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Controllers;

public class QueriesControllerTests
{
    [Fact]
    public async Task Edit_Post_Save_Removes_Stale_Query_Parameters()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Primary",
            ConnectionStringEncrypted = "enc",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select {{foo}}",
            CreatedById = "editor-1",
            UpdatedById = "editor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.QueryParameterDefinitions.AddRange(
            new QueryParameterDefinition
            {
                QueryId = 1,
                Name = "foo",
                Type = QueryParameterType.Text,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new QueryParameterDefinition
            {
                QueryId = 1,
                Name = "bar",
                Type = QueryParameterType.Text,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("editor-1", "Editor"))
        };
        controller.TempData = ControllerTestHelpers.CreateTempData(controller);

        var model = new QueryEditViewModel
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select {{foo}}",
            SubmitAction = "save"
        };

        var result = await controller.Edit(1, model);

        result.Should().BeOfType<RedirectToActionResult>();
        dbContext.QueryParameterDefinitions.Should().HaveCount(1);
        dbContext.QueryParameterDefinitions.Single().Name.Should().Be("foo");
    }

    [Fact]
    public async Task Create_Post_Run_Returns_View_With_ModelError_For_Invalid_Sql()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Primary",
            ConnectionStringEncrypted = "enc",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("editor-1", "Editor"))
        };

        var model = new QueryEditViewModel
        {
            DataSourceId = 1,
            SqlText = "update table set name = 'x'",
            SubmitAction = "run"
        };

        var result = await controller.Create(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ModelState.ContainsKey(nameof(model.SqlText)).Should().BeTrue();
        viewResult.Model.Should().BeAssignableTo<QueryEditViewModel>();
    }

    [Fact]
    public async Task Create_Post_Saves_Query_When_Valid()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Primary",
            ConnectionStringEncrypted = "enc",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("editor-2");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("editor-2", "Editor"))
        };
        controller.TempData = ControllerTestHelpers.CreateTempData(controller);

        var model = new QueryEditViewModel
        {
            Name = "  My Query  ",
            Description = "Test",
            DataSourceId = 1,
            SqlText = "select 1"
        };

        var result = await controller.Create(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(QueriesController.Edit));
        dbContext.Queries.Should().HaveCount(1);
        dbContext.Queries.Single().Name.Should().Be("My Query");
        dbContext.Queries.Single().CreatedById.Should().Be("editor-2");
    }

    [Fact]
    public async Task Edit_Post_Run_Persists_QueryExecution_When_QueryRunner_Fails()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");

        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Broken",
            ConnectionStringEncrypted = protector.Protect("Server=localhost\\INVALID;Database=master;Trusted_Connection=True;Connection Timeout=1;"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
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
        await dbContext.SaveChangesAsync();

        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        dbContext.Users.Add(new IdentityUser { Id = "editor-1", UserName = "editor-1" });
        await dbContext.SaveChangesAsync();
        var userManager = UserManagerMockHelper.Create("editor-1", dbContext);
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("editor-1", "Editor"))
        };

        var model = new QueryEditViewModel
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            SubmitAction = "run"
        };

        var result = await controller.Edit(1, model);

        result.Should().BeOfType<ViewResult>();
        dbContext.QueryExecutions.Should().HaveCount(1);
        var execution = dbContext.QueryExecutions.Single();
        execution.QueryId.Should().Be(1);
        execution.Status.Should().Be(QueryExecutionStatus.Failed);
    }

    [Fact]
    public async Task Details_Returns_Forbid_When_User_Lacks_Permission()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "owner",
            UpdatedById = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create("viewer-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser("viewer-1", "Viewer"))
        };

        var result = await controller.Details(1);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Details_Returns_View_When_User_Is_Shared()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var userId = "viewer-2";
        dbContext.DataSources.Add(new DataSource
        {
            Name = "Primary",
            ConnectionStringEncrypted = "enc",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Shares.Add(new Share
        {
            EntityType = ShareEntityType.Query,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
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
        var share = dbContext.Shares.Single();
        share.EntityId = query.Id;
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var schemaBrowser = new SchemaBrowserService(dbContext, dataProtectionProvider);
        var userManager = UserManagerMockHelper.Create(userId);
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var parameterService = new FakeQueryParameterService();

        var controller = new QueriesController(dbContext, userManager.Object, queryRunner, permissionService, schemaBrowser, parameterService)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(
                ControllerTestHelpers.CreateUser(userId, "Viewer"))
        };

        var result = await controller.Details(query.Id);

        result.Should().BeOfType<ViewResult>();
    }
}
