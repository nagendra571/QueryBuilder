namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationDeleteResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? QueryId { get; set; }
    public IReadOnlyList<string> AffectedDashboards { get; set; } = Array.Empty<string>();
    public bool IsForbidden { get; set; }
}
