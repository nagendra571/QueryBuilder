using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationDetailsViewModel
{
    public Visualization Visualization { get; set; } = new();
    public QueryResultViewModel Result { get; set; } = new();
    public ChartVisualizationConfig ChartConfig { get; set; } = new();
    public ChartVisualizationRenderModel? ChartRender { get; set; }
    public CounterVisualizationConfig CounterConfig { get; set; } = new();
    public CounterVisualizationRenderModel? CounterRender { get; set; }
    public FunnelVisualizationConfig FunnelConfig { get; set; } = new();
    public FunnelVisualizationRenderModel? FunnelRender { get; set; }
    public TableVisualizationConfig? TableConfig { get; set; }
    public string QueryName { get; set; } = string.Empty;
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Dashboards { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
}
