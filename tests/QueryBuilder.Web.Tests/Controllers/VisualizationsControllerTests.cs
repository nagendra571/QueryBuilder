using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Controllers;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Controllers;

public class VisualizationsControllerTests
{
    [Fact]
    public async Task Create_Post_Save_Persists_Visualization_For_Query()
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
        var parameterService = new FakeQueryParameterService();
        var visualizationService = new Mock<IVisualizationService>();
        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var controller = new VisualizationsController(dbContext, queryRunner, new NullLogger<VisualizationsController>(), parameterService, visualizationService.Object, permissionService, userManager.Object);

        var model = new VisualizationEditViewModel
        {
            QueryId = 1,
            Name = "New Visualization",
            Type = VisualizationType.Table,
            SubmitAction = "save"
        };

        var result = await controller.Create(model);

        result.Should().BeOfType<RedirectToActionResult>();
        dbContext.Visualizations.Should().HaveCount(1);
        dbContext.Visualizations.Single().QueryId.Should().Be(1);
    }

    [Fact]
    public async Task Data_Returns_BadRequest_For_Parameter_Validation_Errors()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select {{abc}}",
            CreatedById = "editor-1",
            UpdatedById = "editor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Visualizations.Add(new Visualization
        {
            Id = 1,
            QueryId = 1,
            Name = "Viz",
            Type = VisualizationType.Table,
            ConfigJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        var errors = new List<QueryParameterValidationError>
        {
            new() { Parameter = "abc", Message = "is missing a value." }
        };
        var parameterService = new FakeQueryParameterService(new QueryParameterApplyResult
        {
            Success = false,
            Errors = errors
        });
        var visualizationService = new Mock<IVisualizationService>();
        var userManager = UserManagerMockHelper.Create("editor-1");
        var permissionService = new PermissionService(dbContext, userManager.Object);
        var controller = new VisualizationsController(dbContext, queryRunner, new NullLogger<VisualizationsController>(), parameterService, visualizationService.Object, permissionService, userManager.Object);

        var result = await controller.Data(1);

        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.Should().BeOfType<ParameterValidationErrorResponse>();
        var payload = (ParameterValidationErrorResponse)badRequest.Value!;
        payload.Type.Should().Be("ParameterValidationError");
        payload.Errors.Should().ContainSingle(error => error.Parameter == "abc" && error.Message == "is missing a value.");
    }
}
