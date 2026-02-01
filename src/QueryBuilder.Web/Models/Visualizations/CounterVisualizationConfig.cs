namespace QueryBuilder.Web.Models.Visualizations;

public class CounterVisualizationConfig
{
    public string Type { get; set; } = "counter";
    public CounterGeneralConfig General { get; set; } = new();
    public CounterFormatConfig Format { get; set; } = new();

    public static CounterVisualizationConfig CreateDefault() => new();
}

public class CounterGeneralConfig
{
    public string? Label { get; set; }
    public bool CountRows { get; set; }
    public string? ValueColumn { get; set; }
    public int ValueRow { get; set; } = 1;
    public string? TargetColumn { get; set; }
    public int TargetRow { get; set; } = 1;
}

public class CounterFormatConfig
{
    public string NumberFormat { get; set; } = "0,0";
    public bool ShowTarget { get; set; } = true;
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string PositiveColor { get; set; } = "green";
    public string NegativeColor { get; set; } = "red";
}
