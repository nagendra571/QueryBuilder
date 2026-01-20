namespace QueryBuilder.Web.Models.Queries;

public class SchemaTableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SchemaResultViewModel
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SchemaTableInfo> Tables { get; set; } = new();
}

public class SchemaColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
}

public class SchemaColumnsResultViewModel
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SchemaColumnInfo> Columns { get; set; } = new();
}
