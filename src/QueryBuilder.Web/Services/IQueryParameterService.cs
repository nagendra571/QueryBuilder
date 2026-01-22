using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Services;

public interface IQueryParameterService
{
    Task<QueryParameterApplyResult> ApplyAsync(QueryParameterApplyRequest request);
}

public sealed class QueryParameterApplyRequest
{
    public string Sql { get; set; } = string.Empty;
    public IReadOnlyList<QueryParameterDefinition> Definitions { get; set; } = Array.Empty<QueryParameterDefinition>();
    public IReadOnlyDictionary<string, string?> Values { get; set; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public bool AllowText { get; set; }
}

public sealed class QueryParameterApplyResult
{
    public bool Success { get; set; }
    public string Sql { get; set; } = string.Empty;
    public Dictionary<string, string?> AppliedValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; set; } = new();
}
