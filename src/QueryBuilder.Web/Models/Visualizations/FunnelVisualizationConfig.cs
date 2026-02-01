namespace QueryBuilder.Web.Models.Visualizations;

public class FunnelVisualizationConfig
{
    public string Type { get; set; } = "funnel";
    public string? StepColumn { get; set; }
    public string StepDisplayName { get; set; } = "Steps";
    public string? ValueColumn { get; set; }
    public string ValueDisplayName { get; set; } = "Value";
    public bool AutoSort { get; set; }
    public string? SortByColumn { get; set; }
    public string SortDirection { get; set; } = "desc";
    public bool TreatNullAsZero { get; set; } = true;
    public FunnelFormatConfig Format { get; set; } = new();

    public static FunnelVisualizationConfig CreateDefault() => new();
}

public class FunnelFormatConfig
{
    public string ValueBarColor { get; set; } = "teal";
    public string PreviousBarColor { get; set; } = "gray";
    public string? HeaderTextColor { get; set; }
    public bool AutoColorByStep { get; set; }
    public Dictionary<string, string> StepColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ValueBarHeight { get; set; } = 22;
    public int PreviousBarHeight { get; set; } = 18;
    public int BarRadius { get; set; } = 3;
    public bool ShowValueBar { get; set; } = true;
    public bool ShowPreviousBar { get; set; } = true;
    public string ValueNumberFormat { get; set; } = "0,0";
    public int PercentPrecision { get; set; } = 2;
    public bool ShowPercentSign { get; set; } = true;
    public int CapPercentPrevious { get; set; } = 250;
    public int TopN { get; set; }
    public bool IncludeOthers { get; set; } = true;
    public bool AggregateSteps { get; set; } = true;
    public string Aggregation { get; set; } = "sum";
    public string NullHandling { get; set; } = "zero";
}
