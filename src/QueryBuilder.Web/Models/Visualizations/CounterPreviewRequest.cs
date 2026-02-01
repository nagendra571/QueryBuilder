namespace QueryBuilder.Web.Models.Visualizations;

public class CounterPreviewRequest
{
    public int QueryId { get; set; }
    public int? ExecutionId { get; set; }
    public CounterVisualizationConfig? Config { get; set; }
}
