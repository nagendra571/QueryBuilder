using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardViewModel
{
    public Dashboard Dashboard { get; set; } = new();
    public IReadOnlyList<DashboardWidgetViewModel> Widgets { get; set; } = Array.Empty<DashboardWidgetViewModel>();
}

public class DashboardWidgetViewModel
{
    public DashboardWidget Widget { get; set; } = new();
    public Visualization Visualization { get; set; } = new();
    public VisualizationConfig Config { get; set; } = new();
    public QueryResultViewModel Result { get; set; } = new();
}
