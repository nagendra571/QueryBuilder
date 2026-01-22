namespace QueryBuilder.Domain.Entities;

public enum QueryExecutionStatus
{
    Success = 1,
    Failed = 2
}

public class QueryExecution
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public QueryExecutionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public int DurationMs { get; set; }
    public int? RowCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ParametersJson { get; set; }

    public Query? Query { get; set; }
}
