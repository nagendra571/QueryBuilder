namespace QueryBuilder.Domain.Entities;

public enum DataSourceType
{
    SqlServer = 1
}

public class DataSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataSourceType Type { get; set; } = DataSourceType.SqlServer;
    public string ConnectionStringEncrypted { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
