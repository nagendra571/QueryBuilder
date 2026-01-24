namespace QueryBuilder.Web.Models.Queries;

public sealed class QueryParameterValidationError
{
    public string? Parameter { get; set; }
    public string Message { get; set; } = string.Empty;
}
