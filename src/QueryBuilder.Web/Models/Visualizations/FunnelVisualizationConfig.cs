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

    public static FunnelVisualizationConfig CreateDefault() => new();
}
