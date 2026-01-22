namespace QueryBuilder.Domain.Entities;

public class DashboardWidget
{
    public int Id { get; set; }
    public int DashboardId { get; set; }
    public int VisualizationId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 4;
    public int Height { get; set; } = 4;

    public Dashboard? Dashboard { get; set; }
    public Visualization? Visualization { get; set; }
    public ICollection<DashboardParameterMapping> ParameterMappings { get; set; } = new List<DashboardParameterMapping>();
}
