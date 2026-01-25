namespace QueryBuilder.Web.Models.Queries;

public class QueryExecutionResult
{
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; set; } = Array.Empty<IReadOnlyList<string?>>();
}
