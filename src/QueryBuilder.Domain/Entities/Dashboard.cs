namespace QueryBuilder.Domain.Entities;

public class Dashboard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
}
