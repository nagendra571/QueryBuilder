namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardSaveLayoutRequest
{
    public List<DashboardWidgetSaveModel> Widgets { get; set; } = new();
}
