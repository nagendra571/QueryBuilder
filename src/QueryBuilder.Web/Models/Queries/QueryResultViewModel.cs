namespace QueryBuilder.Web.Models.Queries;

public class QueryResultViewModel
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? RowCount { get; set; }
    public int DurationMs { get; set; }
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; set; } = Array.Empty<IReadOnlyList<string?>>();
}
