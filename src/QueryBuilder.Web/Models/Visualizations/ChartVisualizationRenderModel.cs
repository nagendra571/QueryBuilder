namespace QueryBuilder.Web.Models.Visualizations;

public class ChartVisualizationRenderModel
{
    public bool Success { get; set; } = true;
    public string ChartType { get; set; } = "bar";
    public string? ResolvedXAxisScale { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<ChartSeriesRenderModel> Series { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? Message { get; set; }
}

public class ChartSeriesRenderModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Axis { get; set; } = "left";
    public string Type { get; set; } = "bar";
    public int? ZIndex { get; set; }
    public List<object?> Data { get; set; } = new();
}

public class ChartPoint
{
    public object? X { get; set; }
    public double? Y { get; set; }
    public double? R { get; set; }
}
