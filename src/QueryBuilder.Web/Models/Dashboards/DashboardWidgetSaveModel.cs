namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardWidgetSaveModel
{
    public int? Id { get; set; }
    public int VisualizationId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
