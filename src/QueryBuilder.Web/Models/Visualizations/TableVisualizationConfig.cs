namespace QueryBuilder.Web.Models.Visualizations;

public class TableVisualizationConfig
{
    public string Type { get; set; } = "table";
    public Dictionary<string, TableColumnConfig> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public TableGridConfig Grid { get; set; } = new();
}

public class TableGridConfig
{
    public int PageSize { get; set; } = 25;
}

public class TableColumnConfig
{
    public string Alignment { get; set; } = "left";
    public bool IsVisible { get; set; } = true;
    public bool UseForSearch { get; set; }
    public string DisplayAs { get; set; } = "text";
    public string? NumberFormat { get; set; }
    public bool AllowHtml { get; set; }
    public bool HighlightLinks { get; set; }
    public string? DateTimeFormat { get; set; }
    public string? FalseText { get; set; }
    public string? TrueText { get; set; }
    public string? UrlTemplate { get; set; }
    public string? TextTemplate { get; set; }
    public string? TitleTemplate { get; set; }
    public bool OpenInNewTab { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
}
