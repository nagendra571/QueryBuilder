using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardViewModel
{
    public Dashboard Dashboard { get; set; } = new();
    public IReadOnlyList<DashboardWidgetViewModel> Widgets { get; set; } = Array.Empty<DashboardWidgetViewModel>();
    public string? PublicShareToken { get; set; }
    public bool PublicShareEnabled { get; set; }
    public DateTimeOffset? PublicShareExpiresAt { get; set; }
    public IReadOnlyList<DashboardParameterControlViewModel> ParameterControls { get; set; } = Array.Empty<DashboardParameterControlViewModel>();
    public IReadOnlyList<DashboardWidgetParameterViewModel> WidgetParameters { get; set; } = Array.Empty<DashboardWidgetParameterViewModel>();
    public DashboardParameterStateViewModel ParameterState { get; set; } = new();
}

public class DashboardWidgetViewModel
{
    public DashboardWidget Widget { get; set; } = new();
    public Visualization Visualization { get; set; } = new();
    public VisualizationConfig Config { get; set; } = new();
    public QueryResultViewModel Result { get; set; } = new();
}
