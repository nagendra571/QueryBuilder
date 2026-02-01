namespace QueryBuilder.Web.Models.Visualizations;

public class ChartPreviewRequest
{
    public int QueryId { get; set; }
    public int? ExecutionId { get; set; }
    public ChartVisualizationConfig? Config { get; set; }
}
