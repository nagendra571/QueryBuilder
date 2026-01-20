namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationConfig
{
    public string? XColumn { get; set; }
    public string? YColumn { get; set; }
    public List<string> YColumns { get; set; } = new();
    public string? LabelColumn { get; set; }
    public string? ValueColumn { get; set; }
    public string? GroupByColumn { get; set; }
    public bool ShowLegend { get; set; } = true;
}
