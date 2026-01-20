namespace QueryBuilder.Domain.Entities;

public class Query
{
    public int Id { get; set; }
    public int DataSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public string CreatedById { get; set; } = string.Empty;
    public string UpdatedById { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsArchived { get; set; }

    public DataSource? DataSource { get; set; }
    public ICollection<QueryExecution> Executions { get; set; } = new List<QueryExecution>();
    public ICollection<Visualization> Visualizations { get; set; } = new List<Visualization>();
}
