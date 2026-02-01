namespace QueryBuilder.Web.Models.Visualizations;

public class CounterVisualizationRenderModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? Label { get; set; }
    public string? ValueRaw { get; set; }
    public double? ValueNumber { get; set; }
    public string? TargetRaw { get; set; }
    public double? TargetNumber { get; set; }
    public int? RowCount { get; set; }
}
