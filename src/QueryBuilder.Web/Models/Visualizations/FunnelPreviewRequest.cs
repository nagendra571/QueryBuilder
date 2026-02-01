namespace QueryBuilder.Web.Models.Visualizations;

public class FunnelPreviewRequest
{
    public int QueryId { get; set; }
    public int? ExecutionId { get; set; }
    public FunnelVisualizationConfig? Config { get; set; }
}
