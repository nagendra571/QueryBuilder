namespace QueryBuilder.Domain.Entities;

public enum VisualizationType
{
    Table = 1,
    Line = 2,
    Bar = 3,
    Pie = 4
}

public class Visualization
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public VisualizationType Type { get; set; } = VisualizationType.Table;
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Query? Query { get; set; }
}
