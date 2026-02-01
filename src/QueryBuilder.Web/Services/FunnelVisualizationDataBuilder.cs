using System.Globalization;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class FunnelVisualizationDataBuilder
{
    private const int DefaultPreviewLimit = 50;
    private static readonly string[] StepPalette =
    {
        "#2dd4bf",
        "#2563eb",
        "#16a34a",
        "#f59e0b",
        "#ef4444",
        "#8b5cf6",
        "#14b8a6",
        "#64748b"
    };

    public FunnelVisualizationRenderModel Build(
        FunnelVisualizationConfig? config,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int? limit = null)
    {
        var normalizedConfig = config ?? FunnelVisualizationConfig.CreateDefault();
        NormalizeConfig(normalizedConfig);
        var format = normalizedConfig.Format ?? new FunnelFormatConfig();
        NormalizeFormat(format, normalizedConfig);

        var render = new FunnelVisualizationRenderModel
        {
            StepHeader = normalizedConfig.StepDisplayName,
            ValueHeader = normalizedConfig.ValueDisplayName,
            HeaderTextColor = format.HeaderTextColor,
            ValueBarHeight = format.ValueBarHeight,
            PreviousBarHeight = format.PreviousBarHeight,
            BarRadius = format.BarRadius,
            ShowValueBar = format.ShowValueBar,
            ShowPreviousBar = format.ShowPreviousBar,
            PercentPrecision = format.PercentPrecision,
            ShowPercentSign = format.ShowPercentSign,
            CapPercentPrevious = format.CapPercentPrevious,
            TopN = format.TopN > 0 ? format.TopN : null,
            IncludeOthers = format.IncludeOthers,
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

        var parsedRows = ParseRows(rows, stepIndex, valueIndex, sortIndex, format.NullHandling);
        var resultRows = parsedRows;

        if (format.AggregateSteps)
        {
            resultRows = AggregateRows(parsedRows, format.Aggregation);
            if (normalizedConfig.AutoSort)
            {
                resultRows = SortAggregatedRows(resultRows, normalizedConfig.SortDirection);
            }
        }
        else if (sortIndex.HasValue)
        {
            resultRows = SortRows(parsedRows, normalizedConfig.SortDirection);
        }

        var truncated = false;
        if (format.TopN > 0 && resultRows.Count > format.TopN)
        {
            truncated = true;
            var remaining = resultRows.Skip(format.TopN).ToList();
            resultRows = resultRows.Take(format.TopN).ToList();
            if (format.IncludeOthers)
            {
                var othersValue = remaining.Sum(r => r.Value);
                resultRows.Add(new FunnelRowData("Others", othersValue, 0, resultRows.Count));
            }
        }

        var capped = ApplyLimit(resultRows, limit ?? DefaultPreviewLimit, out var limitTruncated);
        render.Truncated = truncated || limitTruncated;
        render.Rows = BuildRenderRows(capped, format);
        render.Success = true;
        return render;
    }

    private static void NormalizeConfig(FunnelVisualizationConfig config)
    {
        config.Type = "funnel";
        config.StepDisplayName = string.IsNullOrWhiteSpace(config.StepDisplayName) ? "Steps" : config.StepDisplayName;
        config.ValueDisplayName = string.IsNullOrWhiteSpace(config.ValueDisplayName) ? "Value" : config.ValueDisplayName;
        config.SortDirection = string.IsNullOrWhiteSpace(config.SortDirection) ? "desc" : config.SortDirection;
        config.Format ??= new FunnelFormatConfig();
    }

    private static void NormalizeFormat(FunnelFormatConfig format, FunnelVisualizationConfig config)
    {
        format.ValueBarColor = string.IsNullOrWhiteSpace(format.ValueBarColor) ? "teal" : format.ValueBarColor;
        format.PreviousBarColor = string.IsNullOrWhiteSpace(format.PreviousBarColor) ? "gray" : format.PreviousBarColor;
        format.ValueBarHeight = Clamp(format.ValueBarHeight <= 0 ? 22 : format.ValueBarHeight, 8, 40);
        format.PreviousBarHeight = Clamp(format.PreviousBarHeight <= 0 ? 18 : format.PreviousBarHeight, 8, 40);
        format.BarRadius = Clamp(format.BarRadius < 0 ? 3 : format.BarRadius, 0, 12);
        format.ValueNumberFormat = string.IsNullOrWhiteSpace(format.ValueNumberFormat) ? "0,0" : format.ValueNumberFormat;
        format.PercentPrecision = Clamp(format.PercentPrecision, 0, 6);
        format.CapPercentPrevious = Clamp(format.CapPercentPrevious <= 0 ? 250 : format.CapPercentPrevious, 50, 1000);
        format.Aggregation = string.IsNullOrWhiteSpace(format.Aggregation) ? "sum" : format.Aggregation;
        format.NullHandling = string.IsNullOrWhiteSpace(format.NullHandling)
            ? (config.TreatNullAsZero ? "zero" : "skip")
            : format.NullHandling;
    }

    private static List<FunnelRowData> ParseRows(
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int stepIndex,
        int valueIndex,
        int? sortIndex,
        string nullHandling)
    {
        var parsed = new List<FunnelRowData>();
        if (rows == null)
        {
            return parsed;
        }

        var skipNulls = string.Equals(nullHandling, "skip", StringComparison.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var stepLabel = stepIndex < row.Count ? row[stepIndex] : null;
            var valueRaw = valueIndex < row.Count ? row[valueIndex] : null;
            var valueParsed = TryParseNumber(valueRaw);
            if (valueParsed == null && skipNulls)
            {
                continue;
            }
            var value = valueParsed ?? 0d;
            var sortValue = value;
            if (sortIndex.HasValue)
            {
                var sortRaw = sortIndex.Value < row.Count ? row[sortIndex.Value] : null;
                sortValue = TryParseNumber(sortRaw) ?? 0d;
            }
            parsed.Add(new FunnelRowData(stepLabel ?? string.Empty, value, sortValue, i));
        }

        return parsed;
    }

    private static List<FunnelRowData> AggregateRows(
        IReadOnlyList<FunnelRowData> rows,
        string aggregation)
    {
        var groups = new Dictionary<string, FunnelAggregateState>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!groups.TryGetValue(row.Step, out var state))
            {
                state = new FunnelAggregateState(row.Step, row.Index);
                groups[row.Step] = state;
            }
            state.Add(row.Value);
        }

        var list = new List<FunnelRowData>();
        foreach (var state in groups.Values.OrderBy(s => s.FirstIndex))
        {
            var value = state.GetValue(aggregation);
            list.Add(new FunnelRowData(state.Step, value, value, state.FirstIndex));
        }

        return list;
    }

    private static List<FunnelRowData> SortRows(
        IReadOnlyList<FunnelRowData> rows,
        string? sortDirection)
    {
        var isDesc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = isDesc
            ? rows.OrderByDescending(r => r.SortValue).ThenBy(r => r.Index)
            : rows.OrderBy(r => r.SortValue).ThenBy(r => r.Index);

        return ordered.ToList();
    }

    private static List<FunnelRowData> SortAggregatedRows(
        IReadOnlyList<FunnelRowData> rows,
        string? sortDirection)
    {
        var isDesc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = isDesc
            ? rows.OrderByDescending(r => r.Value).ThenBy(r => r.Index)
            : rows.OrderBy(r => r.Value).ThenBy(r => r.Index);

        return ordered.ToList();
    }

    private static List<FunnelRowData> ApplyLimit(
        List<FunnelRowData> rows,
        int limit,
        out bool truncated)
    {
        truncated = rows.Count > limit;
        return truncated ? rows.Take(limit).ToList() : rows;
    }

    private static List<FunnelRowRenderModel> BuildRenderRows(List<FunnelRowData> rows, FunnelFormatConfig format)
    {
        var results = new List<FunnelRowRenderModel>();
        var maxValue = rows.Count > 0 ? rows.Max(r => r.Value) : 0d;
        var previousValue = 0d;

        for (var i = 0; i < rows.Count; i++)
        {
            var value = rows[i].Value;
            var percentMax = maxValue <= 0 ? 0d : value / maxValue * 100d;
            var percentPrevious = i == 0 ? 100d : previousValue <= 0 ? 0d : value / previousValue * 100d;
            var percentPreviousText = i == 0
                ? FormatPercent(100d, format.PercentPrecision, format.ShowPercentSign)
                : previousValue <= 0
                    ? "--"
                    : FormatPercent(percentPrevious, format.PercentPrecision, format.ShowPercentSign);

            results.Add(new FunnelRowRenderModel
            {
                StepLabel = rows[i].Step,
                Value = value,
                PercentMax = percentMax,
                PercentPrevious = percentPrevious,
                ValueText = FormatNumber(value, format.ValueNumberFormat),
                PercentMaxText = FormatPercent(percentMax, format.PercentPrecision, format.ShowPercentSign),
                PercentPreviousText = percentPreviousText,
                BarColor = ResolveStepColor(rows[i].Step, format),
                PreviousBarColor = format.PreviousBarColor
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

    private static string FormatNumber(double value, string format)
    {
        var decimals = 0;
        var dotIndex = format.IndexOf('.');
        if (dotIndex >= 0 && dotIndex < format.Length - 1)
        {
            decimals = format.Substring(dotIndex + 1).Count(c => c == '0');
        }
        return value.ToString($"N{decimals}", CultureInfo.CurrentCulture);
    }

    private static string FormatPercent(double value, int precision, bool showSign)
    {
        var text = value.ToString($"F{precision}", CultureInfo.CurrentCulture);
        return showSign ? $"{text}%" : text;
    }

    private static string ResolveStepColor(string step, FunnelFormatConfig format)
    {
        if (!format.AutoColorByStep)
        {
            return format.ValueBarColor;
        }

        if (format.StepColors != null && format.StepColors.TryGetValue(step, out var value))
        {
            return value;
        }

        var index = Math.Abs(step.GetHashCode()) % StepPalette.Length;
        return StepPalette[index];
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private sealed class FunnelRowData
    {
        public FunnelRowData(string step, double value, double sortValue, int index)
        {
            Step = step;
            Value = value;
            SortValue = sortValue;
            Index = index;
        }

        public string Step { get; }
        public double Value { get; }
        public double SortValue { get; }
        public int Index { get; }
    }

    private sealed class FunnelAggregateState
    {
        public FunnelAggregateState(string step, int firstIndex)
        {
            Step = step;
            FirstIndex = firstIndex;
        }

        public string Step { get; }
        public int FirstIndex { get; }
        public double Sum { get; private set; }
        public double Min { get; private set; } = double.MaxValue;
        public double Max { get; private set; } = double.MinValue;
        public int Count { get; private set; }

        public void Add(double value)
        {
            Sum += value;
            if (value < Min) Min = value;
            if (value > Max) Max = value;
            Count += 1;
        }

        public double GetValue(string aggregation)
        {
            if (Count == 0)
            {
                return 0d;
            }

            return aggregation.ToLowerInvariant() switch
            {
                "avg" => Sum / Count,
                "min" => Min == double.MaxValue ? 0d : Min,
                "max" => Max == double.MinValue ? 0d : Max,
                "count" => Count,
                _ => Sum
            };
        }
    }
}

