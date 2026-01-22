namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationConfig
{
    public string? XColumn { get; set; }
    public string? YColumn { get; set; }
    public List<string> YColumns { get; set; } = new();
    public bool UseHorizontalBars { get; set; }
    public bool UseStackedBars { get; set; }
    public bool UseFloatingBars { get; set; }
    public string? RangeStartColumn { get; set; }
    public string? RangeEndColumn { get; set; }
    public string? LabelColumn { get; set; }
    public string? ValueColumn { get; set; }
    public string? GroupByColumn { get; set; }
    public bool ShowLegend { get; set; } = true;
    public string LineInterpolationMode { get; set; } = "default";
}
