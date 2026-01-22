using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Sharing;

namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardEditViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public IReadOnlyList<DashboardWidgetItemViewModel> Widgets { get; set; } = Array.Empty<DashboardWidgetItemViewModel>();
    public IReadOnlyList<Visualization> AvailableVisualizations { get; set; } = Array.Empty<Visualization>();
    public ShareSectionViewModel ShareSection { get; set; } = new();
    public IReadOnlyList<DashboardParameterControlViewModel> ParameterControls { get; set; } = Array.Empty<DashboardParameterControlViewModel>();
    public IReadOnlyList<DashboardWidgetParameterViewModel> WidgetParameters { get; set; } = Array.Empty<DashboardWidgetParameterViewModel>();
}
