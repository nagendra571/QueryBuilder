using System.Globalization;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class FunnelVisualizationDataBuilder
{
    private const int DefaultPreviewLimit = 50;

    public FunnelVisualizationRenderModel Build(
        FunnelVisualizationConfig? config,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int? limit = null)
    {
        var normalizedConfig = config ?? FunnelVisualizationConfig.CreateDefault();
        NormalizeConfig(normalizedConfig);

        var render = new FunnelVisualizationRenderModel
        {
            StepHeader = normalizedConfig.StepDisplayName,
            ValueHeader = normalizedConfig.ValueDisplayName,
            TotalRows = rows?.Count ?? 0
        };

        if (columns == null || columns.Count == 0)
        {
            render.Errors.Add("No columns available.");
            render.Success = false;
            return render;
        }

        if (string.IsNullOrWhiteSpace(normalizedConfig.StepColumn))
        {
            render.Errors.Add("Step column is required.");
        }
        if (string.IsNullOrWhiteSpace(normalizedConfig.ValueColumn))
        {
            render.Errors.Add("Value column is required.");
        }
        if (render.Errors.Count > 0)
        {
            render.Success = false;
            return render;
        }

        var columnLookup = columns
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        if (!columnLookup.TryGetValue(normalizedConfig.StepColumn!, out var stepIndex))
        {
            render.Errors.Add($"Column '{normalizedConfig.StepColumn}' not found.");
        }
        if (!columnLookup.TryGetValue(normalizedConfig.ValueColumn!, out var valueIndex))
        {
            render.Errors.Add($"Column '{normalizedConfig.ValueColumn}' not found.");
        }

        var sortColumn = normalizedConfig.SortByColumn;
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            sortColumn = normalizedConfig.ValueColumn;
        }

        int? sortIndex = null;
        if (normalizedConfig.AutoSort && !string.IsNullOrWhiteSpace(sortColumn))
        {
            if (columnLookup.TryGetValue(sortColumn, out var idx))
            {
                sortIndex = idx;
            }
            else
            {
                render.Warnings.Add($"Sort column '{sortColumn}' not found. Using query order.");
            }
        }

        if (render.Errors.Count > 0)
        {
            render.Success = false;
            return render;
        }

        var parsedRows = new List<(string Step, double Value)>();
        if (rows != null)
        {
            foreach (var row in rows)
            {
                var stepLabel = stepIndex < row.Count ? row[stepIndex] : null;
                var valueRaw = valueIndex < row.Count ? row[valueIndex] : null;
                var value = TryParseNumber(valueRaw) ?? (normalizedConfig.TreatNullAsZero ? 0d : 0d);
                parsedRows.Add((stepLabel ?? string.Empty, value));
            }
        }

        if (sortIndex.HasValue)
        {
            parsedRows = SortRows(parsedRows, rows, sortIndex.Value, normalizedConfig.SortDirection);
        }

        var capped = ApplyLimit(parsedRows, limit ?? DefaultPreviewLimit, out var truncated);
        render.Truncated = truncated;
        render.Rows = BuildRenderRows(capped);
        render.Success = true;
        return render;
    }

    private static void NormalizeConfig(FunnelVisualizationConfig config)
    {
        config.Type = "funnel";
        config.StepDisplayName = string.IsNullOrWhiteSpace(config.StepDisplayName) ? "Steps" : config.StepDisplayName;
        config.ValueDisplayName = string.IsNullOrWhiteSpace(config.ValueDisplayName) ? "Value" : config.ValueDisplayName;
        config.SortDirection = string.IsNullOrWhiteSpace(config.SortDirection) ? "desc" : config.SortDirection;
    }

    private static List<(string Step, double Value)> SortRows(
        List<(string Step, double Value)> parsedRows,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int sortIndex,
        string? sortDirection)
    {
        var sorted = new List<(string Step, double Value, double SortValue, int Index)>();
        for (var i = 0; i < parsedRows.Count; i++)
        {
            var row = rows[i];
            var raw = sortIndex < row.Count ? row[sortIndex] : null;
            var sortValue = TryParseNumber(raw) ?? 0d;
            sorted.Add((parsedRows[i].Step, parsedRows[i].Value, sortValue, i));
        }

        var isDesc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = isDesc
            ? sorted.OrderByDescending(r => r.SortValue).ThenBy(r => r.Index)
            : sorted.OrderBy(r => r.SortValue).ThenBy(r => r.Index);

        return ordered.Select(r => (r.Step, r.Value)).ToList();
    }

    private static List<(string Step, double Value)> ApplyLimit(
        List<(string Step, double Value)> rows,
        int limit,
        out bool truncated)
    {
        truncated = rows.Count > limit;
        return truncated ? rows.Take(limit).ToList() : rows;
    }

    private static List<FunnelRowRenderModel> BuildRenderRows(List<(string Step, double Value)> rows)
    {
        var results = new List<FunnelRowRenderModel>();
        var maxValue = rows.Count > 0 ? rows.Max(r => r.Value) : 0d;
        var previousValue = 0d;

        for (var i = 0; i < rows.Count; i++)
        {
            var value = rows[i].Value;
            var percentMax = maxValue <= 0 ? 0d : value / maxValue * 100d;
            var percentPrevious = i == 0 ? 100d : previousValue <= 0 ? 0d : value / previousValue * 100d;

            results.Add(new FunnelRowRenderModel
            {
                StepLabel = rows[i].Step,
                Value = value,
                PercentMax = percentMax,
                PercentPrevious = percentPrevious
            });
            previousValue = value;
        }

        return results;
    }

    private static double? TryParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        return null;
    }
}
