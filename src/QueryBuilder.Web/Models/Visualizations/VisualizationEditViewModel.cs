using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationEditViewModel
{
    public int? Id { get; set; }
    public int QueryId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public VisualizationType Type { get; set; } = VisualizationType.Table;

    [Display(Name = "X Column")]
    public string? XColumn { get; set; }

    [Display(Name = "Y Column")]
    public string? YColumn { get; set; }

    [Display(Name = "Y Columns")]
    public List<string> YColumns { get; set; } = new();

    [Display(Name = "Label Column")]
    public string? LabelColumn { get; set; }

    [Display(Name = "Value Column")]
    public string? ValueColumn { get; set; }

    [Display(Name = "Group By")]
    public string? GroupByColumn { get; set; }

    [Display(Name = "Show Legend")]
    public bool ShowLegend { get; set; } = true;

    [Display(Name = "Auto Refresh")]
    public bool IsAutoRefreshEnabled { get; set; }

    [Display(Name = "Refresh Interval (seconds)")]
    public int? AutoRefreshIntervalSeconds { get; set; }

    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public string? SubmitAction { get; set; }
    public QueryResultViewModel? Result { get; set; }
    public IReadOnlyList<QueryParameterDefinitionViewModel> ParameterDefinitions { get; set; } = Array.Empty<QueryParameterDefinitionViewModel>();
    public Dictionary<string, string> ParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
