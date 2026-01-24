namespace QueryBuilder.Web.Models.Queries;

public sealed class ParameterValidationErrorResponse
{
    public string Type { get; set; } = "ParameterValidationError";
    public string? Message { get; set; }
    public IReadOnlyList<QueryParameterValidationError> Errors { get; set; } = Array.Empty<QueryParameterValidationError>();
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; } = false;
}
