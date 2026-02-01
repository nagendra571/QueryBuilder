namespace QueryBuilder.Web.Models.Visualizations;

public class FunnelVisualizationRenderModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string StepHeader { get; set; } = "Steps";
    public string ValueHeader { get; set; } = "Value";
    public int TotalRows { get; set; }
    public List<FunnelRowRenderModel> Rows { get; set; } = new();
    public bool Truncated { get; set; }
}

public class FunnelRowRenderModel
{
    public string StepLabel { get; set; } = string.Empty;
    public double Value { get; set; }
    public double PercentMax { get; set; }
    public double PercentPrevious { get; set; }
}
