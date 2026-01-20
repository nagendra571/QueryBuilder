using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardWidgetItemViewModel
{
    public int Id { get; set; }
    public int VisualizationId { get; set; }
    public string VisualizationName { get; set; } = string.Empty;
    public VisualizationType VisualizationType { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
