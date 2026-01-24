using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class QueryParameterServiceTests
{
    private static QueryParameterService CreateService()
    {
        var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var queryRunner = new QueryRunner(dbContext, dataProtectionProvider);
        return new QueryParameterService(dbContext, queryRunner);
    }

    [Fact]
    public async Task ApplyAsync_Allows_Unconfigured_Text_When_Value_Provided()
    {
        var service = CreateService();
        var request = new QueryParameterApplyRequest
        {
            Sql = "select {{abc}}",
            Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["abc"] = "asd"
            },
            AllowText = true
        };

        var result = await service.ApplyAsync(request);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Sql.Should().Contain("'asd'");
    }

    [Fact]
    public async Task ApplyAsync_Returns_Missing_Value_Error_When_Value_Absent()
    {
        var service = CreateService();
        var request = new QueryParameterApplyRequest
        {
            Sql = "select {{abc}}",
            AllowText = true
        };

        var result = await service.ApplyAsync(request);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Parameter == "abc" && error.Message == "is missing a value.");
    }

    [Fact]
    public async Task ApplyAsync_Returns_All_Missing_Parameter_Errors()
    {
        var service = CreateService();
        var request = new QueryParameterApplyRequest
        {
            Sql = "select {{abc}}, {{foo}}, {{newp}}",
            AllowText = true
        };

        var result = await service.ApplyAsync(request);

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().ContainSingle(error => error.Parameter == "abc" && error.Message == "is missing a value.");
        result.Errors.Should().ContainSingle(error => error.Parameter == "foo" && error.Message == "is missing a value.");
        result.Errors.Should().ContainSingle(error => error.Parameter == "newp" && error.Message == "is missing a value.");
    }

    [Fact]
    public async Task ApplyAsync_Uses_Default_Value_When_Provided()
    {
        var service = CreateService();
        var request = new QueryParameterApplyRequest
        {
            Sql = "select {{amount}}",
            Definitions = new[]
            {
                new QueryParameterDefinition
                {
                    Name = "amount",
                    Type = QueryParameterType.Number,
                    DefaultValue = "5"
                }
            },
            AllowText = true
        };

        var result = await service.ApplyAsync(request);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Sql.Should().Contain("5");
    }

    [Fact]
    public async Task ApplyAsync_Supports_Range_Tokens_Without_Definition()
    {
        var service = CreateService();
        var request = new QueryParameterApplyRequest
        {
            Sql = "select {{dr.start}}, {{dr.end}}",
            Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["dr.start"] = "2024-01-01",
                ["dr.end"] = "2024-01-31"
            },
            AllowText = true
        };

        var result = await service.ApplyAsync(request);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.AppliedValues.Should().ContainKey("dr.start");
        result.AppliedValues.Should().ContainKey("dr.end");
    }
}
