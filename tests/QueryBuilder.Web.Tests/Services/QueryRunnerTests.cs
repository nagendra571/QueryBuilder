using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class QueryRunnerTests
{
    [Fact]
    public async Task RunAsync_Returns_Error_When_DataSource_Missing()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var runner = new QueryRunner(dbContext, dataProtectionProvider);

        var result = await runner.RunAsync(999, "select 1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Data source not found.");
    }

    [Fact]
    public async Task RunAsync_Returns_Error_When_Connection_Fails()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var dataProtectionProvider = DataProtectionProvider.Create("QueryBuilder.Tests");
        var protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");
        var connectionString = "Server=localhost\\INVALID;Database=master;Trusted_Connection=True;Connection Timeout=1;";

        dbContext.DataSources.Add(new DataSource
        {
            Id = 1,
            Name = "Broken",
            ConnectionStringEncrypted = protector.Protect(connectionString),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var runner = new QueryRunner(dbContext, dataProtectionProvider);

        var result = await runner.RunAsync(1, "select 1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
