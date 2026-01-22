using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Models.Visualizations;

public class VisualizationDetailsViewModel
{
    public Visualization Visualization { get; set; } = new();
    public QueryResultViewModel Result { get; set; } = new();
    public VisualizationConfig Config { get; set; } = new();
    public string QueryName { get; set; } = string.Empty;
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Dashboards { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
}
