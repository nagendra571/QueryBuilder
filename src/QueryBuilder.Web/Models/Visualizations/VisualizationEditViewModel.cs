using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationEditViewModel
{
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

    [Display(Name = "Label Column")]
    public string? LabelColumn { get; set; }

    [Display(Name = "Value Column")]
    public string? ValueColumn { get; set; }

    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public string? SubmitAction { get; set; }
    public QueryResultViewModel? Result { get; set; }
}
