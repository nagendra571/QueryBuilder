using System.ComponentModel.DataAnnotations;
using QueryBuilder.Web.Models.Sharing;

namespace QueryBuilder.Web.Models.Queries;

public class QueryEditViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Data Source")]
    public int? DataSourceId { get; set; }

    [Required]
    [Display(Name = "SQL")]
    public string SqlText { get; set; } = string.Empty;

    public string? SubmitAction { get; set; }
    public QueryResultViewModel? Result { get; set; }
    public IReadOnlyList<QueryVisualizationListItemViewModel> Visualizations { get; set; } = Array.Empty<QueryVisualizationListItemViewModel>();
    public ShareSectionViewModel ShareSection { get; set; } = new();
    public IReadOnlyList<QueryParameterDefinitionViewModel> ParameterDefinitions { get; set; } = Array.Empty<QueryParameterDefinitionViewModel>();
    public IReadOnlyList<string> ParsedTokens { get; set; } = Array.Empty<string>();
    public Dictionary<string, string?> ParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
