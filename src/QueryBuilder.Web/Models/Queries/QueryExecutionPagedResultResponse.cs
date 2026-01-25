namespace QueryBuilder.Web.Models.Queries;

public class QueryExecutionPagedResultResponse
{
    public bool Success { get; set; }
    public int QueryId { get; set; }
    public int? ExecutionId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? RowCount { get; set; }
    public bool HasNext { get; set; }
    public IReadOnlyList<QueryExecutionPreviewColumn> Columns { get; set; } = Array.Empty<QueryExecutionPreviewColumn>();
    public IReadOnlyList<Dictionary<string, string?>> Rows { get; set; } = Array.Empty<Dictionary<string, string?>>();
    public string? ErrorMessage { get; set; }
}
