using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Services;

public class QueryExecutionPreviewService
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _dbContext;

    public QueryExecutionPreviewService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QueryExecutionPreviewResponse> GetLatestAsync(int queryId, int? executionId, int maxRows)
    {
        var execution = await ResolveExecutionAsync(queryId, executionId);
        if (execution == null || string.IsNullOrWhiteSpace(execution.ResultJson))
        {
            return new QueryExecutionPreviewResponse
            {
                Success = false,
                QueryId = queryId,
                ExecutionId = execution?.Id,
                ErrorMessage = "No stored execution results found."
            };
        }

        QueryExecutionResult? result;
        try
        {
            result = JsonSerializer.Deserialize<QueryExecutionResult>(execution.ResultJson, ResultJsonOptions);
        }
        catch (JsonException)
        {
            return new QueryExecutionPreviewResponse
            {
                Success = false,
                QueryId = queryId,
                ExecutionId = execution.Id,
                ErrorMessage = "Stored execution results could not be read."
            };
        }

        var columns = result?.Columns ?? Array.Empty<string>();
        var rows = result?.Rows ?? Array.Empty<IReadOnlyList<string?>>();
        if (maxRows > 0 && rows.Count > maxRows)
        {
            rows = rows.Take(maxRows).ToList();
        }

        return new QueryExecutionPreviewResponse
        {
            Success = true,
            QueryId = queryId,
            ExecutionId = execution.Id,
            Columns = columns.Select(name => new QueryExecutionPreviewColumn { Name = name }).ToList(),
            Rows = BuildRowObjects(columns, rows)
        };
    }

    public async Task<QueryExecutionPagedResultResponse> GetPageAsync(int queryId, int? executionId, int page, int pageSize)
    {
        var execution = await ResolveExecutionAsync(queryId, executionId);
        if (execution == null || string.IsNullOrWhiteSpace(execution.ResultJson))
        {
            return new QueryExecutionPagedResultResponse
            {
                Success = false,
                QueryId = queryId,
                ExecutionId = execution?.Id,
                Page = page,
                PageSize = pageSize,
                ErrorMessage = "No stored execution results found."
            };
        }

        QueryExecutionResult? result;
        try
        {
            result = JsonSerializer.Deserialize<QueryExecutionResult>(execution.ResultJson, ResultJsonOptions);
        }
        catch (JsonException)
        {
            return new QueryExecutionPagedResultResponse
            {
                Success = false,
                QueryId = queryId,
                ExecutionId = execution.Id,
                Page = page,
                PageSize = pageSize,
                ErrorMessage = "Stored execution results could not be read."
            };
        }

        var columns = result?.Columns ?? Array.Empty<string>();
        var rows = result?.Rows ?? Array.Empty<IReadOnlyList<string?>>();
        var start = Math.Max(0, (page - 1) * pageSize);
        var pageRows = rows.Skip(start).Take(pageSize).ToList();
        var hasNext = rows.Count > start + pageRows.Count;

        return new QueryExecutionPagedResultResponse
        {
            Success = true,
            QueryId = queryId,
            ExecutionId = execution.Id,
            Page = page,
            PageSize = pageSize,
            RowCount = execution.RowCount,
            HasNext = hasNext,
            Columns = columns.Select(name => new QueryExecutionPreviewColumn { Name = name }).ToList(),
            Rows = BuildRowObjects(columns, pageRows)
        };
    }

    private async Task<QueryExecution?> ResolveExecutionAsync(int queryId, int? executionId)
    {
        if (executionId.HasValue)
        {
            var exact = await _dbContext.QueryExecutions
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Id == executionId.Value &&
                    e.QueryId == queryId &&
                    e.Status == QueryExecutionStatus.Success);
            if (exact != null)
            {
                return exact;
            }
        }

        return await _dbContext.QueryExecutions
            .AsNoTracking()
            .Where(e => e.QueryId == queryId && e.Status == QueryExecutionStatus.Success && e.ResultJson != null)
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefaultAsync();
    }

    private static IReadOnlyList<Dictionary<string, string?>> BuildRowObjects(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var results = new List<Dictionary<string, string?>>();
        foreach (var row in rows)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                var value = i < row.Count ? row[i] : null;
                map[columns[i]] = value;
            }
            results.Add(map);
        }

        return results;
    }
}
