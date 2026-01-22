using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.Queries;

public class QueryParameterDefinitionInputModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    [Required]
    public QueryParameterType Type { get; set; } = QueryParameterType.Text;

    public string? DefaultValue { get; set; }

    public string SettingsJson { get; set; } = "{}";

    public bool IsRequired { get; set; }
}
