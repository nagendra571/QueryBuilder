namespace QueryBuilder.Web.Models.Queries;

public class QueryListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataSourceName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
