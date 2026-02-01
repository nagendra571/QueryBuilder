namespace QueryBuilder.Web.Models.Visualizations;

public class ChartVisualizationConfig
{
    public string Type { get; set; } = "chart";
    public ChartGeneralConfig General { get; set; } = new();
    public ChartXAxisConfig XAxis { get; set; } = new();
    public ChartYAxisConfig YAxis { get; set; } = new();
    public List<ChartSeriesConfig> Series { get; set; } = new();
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ChartDataLabelsConfig DataLabels { get; set; } = new();

    public static ChartVisualizationConfig CreateDefault() => new();
}

public class ChartGeneralConfig
{
    public string ChartType { get; set; } = "bar";
    public string? XColumn { get; set; }
    public List<string> YColumns { get; set; } = new();
    public string? GroupBy { get; set; }
    public string? ErrorsColumn { get; set; }
    public bool ShowLegend { get; set; } = true;
    public string Stacking { get; set; } = "disabled";
    public bool NormalizeToPercent { get; set; }
    public bool NullAsZero { get; set; } = true;
}

public class ChartXAxisConfig
{
    public string Scale { get; set; } = "auto";
    public string? Name { get; set; }
    public bool SortValues { get; set; } = true;
    public bool ReverseOrder { get; set; }
    public bool ShowLabels { get; set; } = true;
}

public class ChartYAxisConfig
{
    public ChartAxisSettings Left { get; set; } = new();
    public ChartAxisSettings Right { get; set; } = new();
}

public class ChartAxisSettings
{
    public string Scale { get; set; } = "linear";
    public string? Name { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool Reverse { get; set; }
}

public class ChartSeriesConfig
{
    public string Key { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string Axis { get; set; } = "left";
    public string? Type { get; set; }
    public int? ZIndex { get; set; }
}

public class ChartDataLabelsConfig
{
    public bool Enabled { get; set; }
    public string NumberFormat { get; set; } = "0,0.00";
    public string PercentFormat { get; set; } = "0[.]00%";
    public string DateTimeFormat { get; set; } = "DD/MM/YY HH:mm";
    public string LabelTemplate { get; set; } = "auto";
}
