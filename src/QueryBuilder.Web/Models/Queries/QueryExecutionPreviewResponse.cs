namespace QueryBuilder.Web.Models.Queries;

public class QueryExecutionPreviewResponse
{
    public bool Success { get; set; }
    public int QueryId { get; set; }
    public int? ExecutionId { get; set; }
    public IReadOnlyList<QueryExecutionPreviewColumn> Columns { get; set; } = Array.Empty<QueryExecutionPreviewColumn>();
    public IReadOnlyList<Dictionary<string, string?>> Rows { get; set; } = Array.Empty<Dictionary<string, string?>>();
    public string? ErrorMessage { get; set; }
}

public class QueryExecutionPreviewColumn
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
}
