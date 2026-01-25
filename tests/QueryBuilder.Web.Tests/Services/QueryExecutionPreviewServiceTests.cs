using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class QueryExecutionPreviewServiceTests
{
    [Fact]
    public async Task GetLatestAsync_Returns_Latest_Success_With_ResultJson()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor",
            UpdatedById = "editor",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var older = new QueryExecution
        {
            QueryId = 1,
            Status = QueryExecutionStatus.Success,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DurationMs = 10,
            ResultJson = JsonSerializer.Serialize(new QueryExecutionResult
            {
                Columns = new[] { "id" },
                Rows = new List<IReadOnlyList<string?>> { new string?[] { "1" } }
            })
        };
        var latest = new QueryExecution
        {
            QueryId = 1,
            Status = QueryExecutionStatus.Success,
            StartedAt = DateTimeOffset.UtcNow,
            DurationMs = 5,
            ResultJson = JsonSerializer.Serialize(new QueryExecutionResult
            {
                Columns = new[] { "id", "name" },
                Rows = new List<IReadOnlyList<string?>> { new string?[] { "2", "Apple" } }
            })
        };
        dbContext.QueryExecutions.AddRange(older, latest);
        await dbContext.SaveChangesAsync();

        var service = new QueryExecutionPreviewService(dbContext);

        var result = await service.GetLatestAsync(1, null, 50);

        result.Success.Should().BeTrue();
        result.ExecutionId.Should().Be(latest.Id);
        result.Columns.Should().HaveCount(2);
        result.Rows.Should().ContainSingle(row => row["name"] == "Apple");
    }

    [Fact]
    public async Task GetLatestAsync_Respects_MaxRows()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor",
            UpdatedById = "editor",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.QueryExecutions.Add(new QueryExecution
        {
            QueryId = 1,
            Status = QueryExecutionStatus.Success,
            StartedAt = DateTimeOffset.UtcNow,
            DurationMs = 5,
            ResultJson = JsonSerializer.Serialize(new QueryExecutionResult
            {
                Columns = new[] { "id" },
                Rows = new List<IReadOnlyList<string?>>
                {
                    new string?[] { "1" },
                    new string?[] { "2" },
                    new string?[] { "3" }
                }
            })
        });
        await dbContext.SaveChangesAsync();

        var service = new QueryExecutionPreviewService(dbContext);

        var result = await service.GetLatestAsync(1, null, 2);

        result.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPageAsync_Returns_HasNext_For_Stored_Rows()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.Queries.Add(new Query
        {
            Id = 1,
            Name = "Query",
            DataSourceId = 1,
            SqlText = "select 1",
            CreatedById = "editor",
            UpdatedById = "editor",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.QueryExecutions.Add(new QueryExecution
        {
            QueryId = 1,
            Status = QueryExecutionStatus.Success,
            StartedAt = DateTimeOffset.UtcNow,
            DurationMs = 5,
            ResultJson = JsonSerializer.Serialize(new QueryExecutionResult
            {
                Columns = new[] { "id" },
                Rows = new List<IReadOnlyList<string?>>
                {
                    new string?[] { "1" },
                    new string?[] { "2" },
                    new string?[] { "3" }
                }
            })
        });
        await dbContext.SaveChangesAsync();

        var service = new QueryExecutionPreviewService(dbContext);

        var result = await service.GetPageAsync(1, null, 1, 2);

        result.Rows.Should().HaveCount(2);
        result.HasNext.Should().BeTrue();
    }
}
