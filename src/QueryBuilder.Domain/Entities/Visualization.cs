namespace QueryBuilder.Domain.Entities;

public enum VisualizationType
{
    Table = 1,
    Line = 2,
    Bar = 3,
    Pie = 4,
    FloatingBar = 5,
    HorizontalBar = 6
}

public class Visualization
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public VisualizationType Type { get; set; } = VisualizationType.Table;
    public string ConfigJson { get; set; } = "{}";
    public bool IsAutoRefreshEnabled { get; set; }
    public int? AutoRefreshIntervalSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public Query? Query { get; set; }
}
