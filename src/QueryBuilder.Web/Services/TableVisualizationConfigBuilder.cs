using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class TableVisualizationConfigBuilder
{
    public TableVisualizationConfig Build(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        TableVisualizationConfig? existing = null)
    {
        var config = existing ?? new TableVisualizationConfig();
        var merged = new TableVisualizationConfig
        {
            Type = "table",
            Grid = config.Grid ?? new TableGridConfig(),
            Columns = new Dictionary<string, TableColumnConfig>(StringComparer.OrdinalIgnoreCase)
        };

        for (var i = 0; i < columns.Count; i++)
        {
            var columnName = columns[i];
            if (config.Columns.TryGetValue(columnName, out var existingColumn))
            {
                merged.Columns[columnName] = existingColumn;
                continue;
            }

            var displayAs = InferDisplayAs(rows, i);
            merged.Columns[columnName] = BuildDefaultColumn(displayAs);
        }

        return merged;
    }

    private static TableColumnConfig BuildDefaultColumn(string displayAs)
    {
        var config = new TableColumnConfig
        {
            DisplayAs = displayAs,
            Alignment = displayAs == "number" ? "right" : displayAs == "boolean" ? "center" : "left",
            IsVisible = true,
            UseForSearch = displayAs == "text" || displayAs == "json",
            FalseText = "False",
            TrueText = "True"
        };

        if (displayAs == "number")
        {
            config.NumberFormat = "0,0";
        }

        return config;
    }

    private static string InferDisplayAs(IReadOnlyList<IReadOnlyList<string?>> rows, int columnIndex)
    {
        var values = new List<string>();
        foreach (var row in rows)
        {
            if (columnIndex >= row.Count)
            {
                continue;
            }

            var value = row[columnIndex];
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        if (values.Count == 0)
        {
            return "text";
        }

        if (values.All(IsBoolean))
        {
            return "boolean";
        }

        if (values.All(IsNumber))
        {
            return "number";
        }

        if (values.All(IsDateTime))
        {
            return "datetime";
        }

        return "text";
    }

    private static bool IsBoolean(string value)
    {
        return bool.TryParse(value, out _) ||
               value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumber(string value)
    {
        return decimal.TryParse(value, out _);
    }

    private static bool IsDateTime(string value)
    {
        return DateTime.TryParse(value, out _);
    }
}
