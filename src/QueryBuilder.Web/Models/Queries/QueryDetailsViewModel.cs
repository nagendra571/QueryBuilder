namespace QueryBuilder.Web.Models.Queries;

public class QueryDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public string SqlText { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public IReadOnlyList<QueryVisualizationListItemViewModel> Visualizations { get; set; } = Array.Empty<QueryVisualizationListItemViewModel>();
}
