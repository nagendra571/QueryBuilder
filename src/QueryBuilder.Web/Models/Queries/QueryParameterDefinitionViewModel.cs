using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.Queries;

public class QueryParameterDefinitionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public QueryParameterType Type { get; set; } = QueryParameterType.Text;
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public IReadOnlyList<QueryParameterOptionViewModel> Options { get; set; } = Array.Empty<QueryParameterOptionViewModel>();
}

public class QueryParameterOptionViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
