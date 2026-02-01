namespace QueryBuilder.Web.Models.Visualizations;

public class FunnelVisualizationRenderModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string StepHeader { get; set; } = "Steps";
    public string ValueHeader { get; set; } = "Value";
    public string? HeaderTextColor { get; set; }
    public int ValueBarHeight { get; set; }
    public int PreviousBarHeight { get; set; }
    public int BarRadius { get; set; }
    public bool ShowValueBar { get; set; }
    public bool ShowPreviousBar { get; set; }
    public int PercentPrecision { get; set; }
    public bool ShowPercentSign { get; set; }
    public int CapPercentPrevious { get; set; }
    public int? TopN { get; set; }
    public bool IncludeOthers { get; set; }
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
    public string? ValueText { get; set; }
    public string? PercentMaxText { get; set; }
    public string? PercentPreviousText { get; set; }
    public string? BarColor { get; set; }
    public string? PreviousBarColor { get; set; }
}
