namespace QueryBuilder.Domain.Entities;

public enum QueryParameterType
{
    Text = 1,
    Number = 2,
    Dropdown = 3,
    Date = 4,
    DateTime = 5,
    DateRange = 6,
    DateTimeRange = 7
}

public enum DashboardParameterPlacement
{
    TopBar = 1,
    Inline = 2,
    Hidden = 3
}

public enum DashboardParameterBindingType
{
    DashboardControlKey = 1,
    InlineControl = 2,
    StaticValue = 3
}

public class QueryParameterDefinition
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public QueryParameterType Type { get; set; } = QueryParameterType.Text;
    public string? DefaultValue { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public bool IsRequired { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Query? Query { get; set; }
}

public class DashboardParameterControl
{
    public int Id { get; set; }
    public int DashboardId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public QueryParameterType Type { get; set; } = QueryParameterType.Text;
    public string? DefaultValue { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DashboardParameterPlacement Placement { get; set; } = DashboardParameterPlacement.TopBar;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Dashboard? Dashboard { get; set; }
}

public class DashboardParameterMapping
{
    public int Id { get; set; }
    public int DashboardWidgetId { get; set; }
    public int QueryId { get; set; }
    public string QueryParamKey { get; set; } = string.Empty;
    public DashboardParameterBindingType BindingType { get; set; } = DashboardParameterBindingType.DashboardControlKey;
    public string? StaticValue { get; set; }
    public string? ControlKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public DashboardWidget? DashboardWidget { get; set; }
    public Query? Query { get; set; }
}
