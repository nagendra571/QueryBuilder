using System.ComponentModel.DataAnnotations;

namespace QueryBuilder.Web.Models.Visualizations;

public class AddVisualizationToDashboardInputModel
{
    [Required]
    public int VisualizationId { get; set; }

    [Required]
    public int DashboardId { get; set; }
}
