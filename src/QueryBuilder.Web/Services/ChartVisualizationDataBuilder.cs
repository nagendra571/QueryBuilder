using System.Globalization;
using System.Linq;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class ChartVisualizationDataBuilder
{
    private static readonly HashSet<string> SupportedChartTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "line",
        "bar",
        "area",
        "pie",
        "scatter",
        "bubble",
        "heatmap",
        "box"
    };

    public ChartVisualizationRenderModel Build(
        ChartVisualizationConfig? config,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var normalizedConfig = config ?? ChartVisualizationConfig.CreateDefault();
        NormalizeConfig(normalizedConfig);

        var chartType = NormalizeChartType(normalizedConfig.General.ChartType);
        var render = new ChartVisualizationRenderModel
        {
            ChartType = chartType
        };

        if (!SupportedChartTypes.Contains(chartType))
        {
            render.Success = false;
            render.Errors.Add("Chart type is not supported.");
            return render;
        }

        if (chartType is "heatmap" or "box")
        {
            render.Success = false;
            render.Errors.Add("Not supported yet.");
            render.Message = "Not supported yet.";
            return render;
        }

        if (columns.Count == 0)
        {
            render.Success = false;
            render.Errors.Add("No columns available to build this chart.");
            return render;
        }

        var columnLookup = BuildColumnLookup(columns);
        var xColumn = normalizedConfig.General.XColumn?.Trim();
        var yColumns = normalizedConfig.General.YColumns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groupBy = string.IsNullOrWhiteSpace(normalizedConfig.General.GroupBy)
            ? null
            : normalizedConfig.General.GroupBy!.Trim();

        if (chartType != "pie" && string.IsNullOrWhiteSpace(xColumn))
        {
            render.Errors.Add("X column is required.");
        }

        if (chartType == "pie" && string.IsNullOrWhiteSpace(xColumn) && string.IsNullOrWhiteSpace(groupBy))
        {
            render.Errors.Add("Select an X column or Group By for pie charts.");
        }

        if (yColumns.Count == 0)
        {
            render.Errors.Add("Select at least one Y column.");
        }

        if (render.Errors.Count > 0)
        {
            render.Success = false;
            return render;
        }

        var xIndex = !string.IsNullOrWhiteSpace(xColumn) && columnLookup.TryGetValue(xColumn!, out var xIdx)
            ? xIdx
            : -1;

        if (chartType != "pie" && xIndex < 0)
        {
            render.Success = false;
            render.Errors.Add($"X column '{xColumn}' was not found.");
            return render;
        }

        var yIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in yColumns)
        {
            if (columnLookup.TryGetValue(col, out var idx))
            {
                yIndexes[col] = idx;
            }
            else
            {
                render.Warnings.Add($"Y column '{col}' was not found.");
            }
        }

        if (yIndexes.Count == 0)
        {
            render.Success = false;
            render.Errors.Add("No valid Y columns found in the result set.");
            return render;
        }

        var groupIndex = groupBy != null && columnLookup.TryGetValue(groupBy, out var gIdx) ? gIdx : -1;
        if (groupBy != null && groupIndex < 0)
        {
            render.Warnings.Add($"Group By column '{groupBy}' was not found. Grouping disabled.");
            groupBy = null;
        }

        if (!string.IsNullOrWhiteSpace(normalizedConfig.General.ErrorsColumn))
        {
            var errorsColumn = normalizedConfig.General.ErrorsColumn.Trim();
            if (columnLookup.TryGetValue(errorsColumn, out _))
            {
                render.Warnings.Add("Errors column is stored but not rendered yet.");
            }
            else
            {
                render.Warnings.Add($"Errors column '{errorsColumn}' was not found.");
            }
        }

        var resolvedXAxisScale = ResolveXAxisScale(normalizedConfig.XAxis.Scale, rows, xIndex);
        render.ResolvedXAxisScale = resolvedXAxisScale;

        if (chartType == "pie")
        {
            BuildPie(render, normalizedConfig, rows, xIndex, yIndexes, groupIndex);
            return render;
        }

        if (chartType is "scatter" or "bubble")
        {
            BuildScatter(render, normalizedConfig, rows, xIndex, yIndexes, groupIndex, resolvedXAxisScale, groupBy);
            return render;
        }

        BuildStandard(render, normalizedConfig, rows, xIndex, yIndexes, groupIndex, resolvedXAxisScale, groupBy);
        return render;
    }

    private static void BuildPie(
        ChartVisualizationRenderModel render,
        ChartVisualizationConfig config,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int xIndex,
        Dictionary<string, int> yIndexes,
        int groupIndex)
    {
        var yColumn = yIndexes.Keys.First();
        if (yIndexes.Count > 1)
        {
            render.Warnings.Add("Pie charts use the first selected Y column only.");
        }

        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var label = ResolveLabel(row, xIndex, groupIndex);
            var value = ReadNumber(row, yIndexes[yColumn]);
            if (value == null)
            {
                if (config.General.NullAsZero)
                {
                    value = 0;
                }
                else
                {
                    continue;
                }
            }

            totals[label] = totals.TryGetValue(label, out var existing) ? existing + value.Value : value.Value;
        }

        render.Labels = totals.Keys.ToList();
        var series = new ChartSeriesRenderModel
        {
            Key = yColumn,
            Label = yColumn,
            Axis = "left",
            Type = "pie",
            Data = totals.Values.Cast<object?>().ToList()
        };
        ApplySeriesOverrides(config, series);
        render.Series.Add(series);
    }

    private static void BuildScatter(
        ChartVisualizationRenderModel render,
        ChartVisualizationConfig config,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int xIndex,
        Dictionary<string, int> yIndexes,
        int groupIndex,
        string resolvedXAxisScale,
        string? groupBy)
    {
        var seriesDefinitions = BuildSeriesDefinitions(config, yIndexes, rows, groupIndex, groupBy);
        var seriesLookup = seriesDefinitions.ToDictionary(
            s => s.Key,
            s => s.ToSeriesRenderModel(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var groupValue = groupIndex >= 0 ? row.ElementAtOrDefault(groupIndex) : null;
            var labelGroup = groupIndex >= 0 ? groupValue ?? string.Empty : null;
            var xValue = ResolveXValue(row.ElementAtOrDefault(xIndex), resolvedXAxisScale);
            if (xValue == null)
            {
                continue;
            }

            foreach (var definition in seriesDefinitions)
            {
                if (groupIndex >= 0 && !string.Equals(definition.GroupValue, labelGroup, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var yValue = ReadNumber(row, definition.YIndex);
                if (yValue == null)
                {
                    if (config.General.NullAsZero)
                    {
                        yValue = 0;
                    }
                    else
                    {
                        continue;
                    }
                }

                var point = new ChartPoint
                {
                    X = xValue,
                    Y = yValue
                };

                if (render.ChartType == "bubble")
                {
                    point.R = 5;
                }

                seriesLookup[definition.Key].Data.Add(point);
            }
        }

        render.Series.AddRange(seriesDefinitions.Select(def => seriesLookup[def.Key]));
    }

    private static void BuildStandard(
        ChartVisualizationRenderModel render,
        ChartVisualizationConfig config,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int xIndex,
        Dictionary<string, int> yIndexes,
        int groupIndex,
        string resolvedXAxisScale,
        string? groupBy)
    {
        var seriesDefinitions = BuildSeriesDefinitions(config, yIndexes, rows, groupIndex, groupBy);
        var seriesLookup = seriesDefinitions.ToDictionary(
            s => s.Key,
            s => s.ToSeriesRenderModel(),
            StringComparer.OrdinalIgnoreCase);
        var valuesByLabel = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var label = ResolveLabel(row, xIndex, -1);
            if (!valuesByLabel.TryGetValue(label, out var values))
            {
                values = new double?[seriesDefinitions.Count];
                valuesByLabel[label] = values;
            }

            var groupValue = groupIndex >= 0 ? row.ElementAtOrDefault(groupIndex) : null;
            for (var i = 0; i < seriesDefinitions.Count; i++)
            {
                var definition = seriesDefinitions[i];
                if (groupIndex >= 0 && !string.Equals(definition.GroupValue, groupValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = ReadNumber(row, definition.YIndex);
                if (value == null)
                {
                    if (config.General.NullAsZero)
                    {
                        value = 0;
                    }
                    else
                    {
                        continue;
                    }
                }

                values[i] = values[i].GetValueOrDefault() + value.Value;
            }
        }

        var labels = valuesByLabel.Keys.ToList();
        if (config.XAxis.SortValues)
        {
            labels = SortLabels(labels, resolvedXAxisScale);
        }
        if (config.XAxis.ReverseOrder)
        {
            labels.Reverse();
        }

        render.Labels = labels;

        for (var i = 0; i < seriesDefinitions.Count; i++)
        {
            var def = seriesDefinitions[i];
            var series = seriesLookup[def.Key];
            foreach (var label in labels)
            {
                var value = valuesByLabel.TryGetValue(label, out var values) ? values[i] : null;
                series.Data.Add(value);
            }
        }

        var seriesList = seriesDefinitions.Select(def => seriesLookup[def.Key]).ToList();
        if (config.General.NullAsZero)
        {
            ReplaceNullsWithZero(seriesList);
        }
        if (config.General.NormalizeToPercent)
        {
            NormalizeSeriesToPercent(seriesList);
        }

        render.Series.AddRange(seriesList);
    }

    private static List<string> SortLabels(List<string> labels, string resolvedXAxisScale)
    {
        if (resolvedXAxisScale == "datetime")
        {
            return labels.OrderBy(value => TryParseDate(value, out var date) ? date : DateTimeOffset.MinValue).ToList();
        }

        if (resolvedXAxisScale is "linear" or "logarithmic")
        {
            return labels.OrderBy(value => TryParseNumber(value, out var number) ? number : double.MinValue).ToList();
        }

        return labels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void NormalizeSeriesToPercent(List<ChartSeriesRenderModel> series)
    {
        if (series.Count == 0 || series[0].Data.Count == 0)
        {
            return;
        }

        var pointCount = series[0].Data.Count;
        for (var i = 0; i < pointCount; i++)
        {
            var sum = 0d;
            foreach (var item in series)
            {
                if (item.Data[i] is double value)
                {
                    sum += value;
                }
            }

            foreach (var item in series)
            {
                if (item.Data[i] is double value)
                {
                    item.Data[i] = sum > 0 ? (value / sum) * 100d : 0d;
                }
            }
        }
    }

    private static void ReplaceNullsWithZero(List<ChartSeriesRenderModel> series)
    {
        foreach (var item in series)
        {
            for (var i = 0; i < item.Data.Count; i++)
            {
                if (item.Data[i] == null)
                {
                    item.Data[i] = 0d;
                }
            }
        }
    }

    private static List<SeriesDefinition> BuildSeriesDefinitions(
        ChartVisualizationConfig config,
        Dictionary<string, int> yIndexes,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int groupIndex,
        string? groupBy)
    {
        // When group-by is set, each (yColumn, groupValue) pair becomes its own series.
        var results = new List<SeriesDefinition>();
        var groupValues = groupIndex >= 0
            ? rows.Select(row => row.ElementAtOrDefault(groupIndex) ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        var yColumns = yIndexes.Keys.ToList();

        foreach (var yColumn in yColumns)
        {
            if (groupIndex >= 0)
            {
                foreach (var groupValue in groupValues)
                {
                    var label = yColumns.Count > 1 ? $"{groupValue} {yColumn}".Trim() : groupValue;
                    results.Add(new SeriesDefinition
                    {
                        Key = $"{yColumn}|{groupBy}={groupValue}",
                        Label = label,
                        YColumn = yColumn,
                        GroupValue = groupValue,
                        YIndex = yIndexes[yColumn]
                    });
                }
            }
            else
            {
                results.Add(new SeriesDefinition
                {
                    Key = yColumn,
                    Label = yColumn,
                    YColumn = yColumn,
                    GroupValue = null,
                    YIndex = yIndexes[yColumn]
                });
            }
        }

        var overrides = config.Series
            .Where(s => !string.IsNullOrWhiteSpace(s.Key))
            .ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var def in results)
        {
            var seriesKey = BuildSeriesKey(def, groupIndex, config, groupBy);
            var label = def.Label;
            var axis = "left";
            var type = config.General.ChartType;
            int? zIndex = null;

            if (overrides.TryGetValue(seriesKey, out var overrideConfig))
            {
                if (!string.IsNullOrWhiteSpace(overrideConfig.Label))
                {
                    label = overrideConfig.Label!.Trim();
                }
                if (!string.IsNullOrWhiteSpace(overrideConfig.Axis))
                {
                    axis = overrideConfig.Axis!.Trim();
                }
                if (!string.IsNullOrWhiteSpace(overrideConfig.Type))
                {
                    type = overrideConfig.Type!.Trim();
                }
                if (overrideConfig.ZIndex.HasValue)
                {
                    zIndex = overrideConfig.ZIndex.Value;
                }
            }

            def.Key = seriesKey;
            def.Label = label;
            def.Axis = axis;
            def.Type = type;
            def.ZIndex = zIndex;
        }

        return results;
    }

    private static void ApplySeriesOverrides(ChartVisualizationConfig config, ChartSeriesRenderModel series)
    {
        var overrideConfig = config.Series.FirstOrDefault(s => s.Key.Equals(series.Key, StringComparison.OrdinalIgnoreCase));
        if (overrideConfig == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(overrideConfig.Label))
        {
            series.Label = overrideConfig.Label!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(overrideConfig.Axis))
        {
            series.Axis = overrideConfig.Axis!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(overrideConfig.Type))
        {
            series.Type = overrideConfig.Type!.Trim();
        }
        if (overrideConfig.ZIndex.HasValue)
        {
            series.ZIndex = overrideConfig.ZIndex.Value;
        }
    }

    private static Dictionary<string, int> BuildColumnLookup(IReadOnlyList<string> columns)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            lookup[columns[i]] = i;
        }

        return lookup;
    }

    private static string NormalizeChartType(string? chartType)
    {
        return string.IsNullOrWhiteSpace(chartType) ? "bar" : chartType.Trim().ToLowerInvariant();
    }

    private static void NormalizeConfig(ChartVisualizationConfig config)
    {
        config.General ??= new ChartGeneralConfig();
        config.General.ChartType = NormalizeChartType(config.General.ChartType);
        config.XAxis ??= new ChartXAxisConfig();
        config.YAxis ??= new ChartYAxisConfig();
        config.YAxis.Left ??= new ChartAxisSettings();
        config.YAxis.Right ??= new ChartAxisSettings();
        config.Series ??= new List<ChartSeriesConfig>();
        config.Colors ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        config.DataLabels ??= new ChartDataLabelsConfig();
    }

    private static string ResolveXAxisScale(string scale, IReadOnlyList<IReadOnlyList<string?>> rows, int xIndex)
    {
        if (!string.Equals(scale, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return scale.Trim().ToLowerInvariant();
        }

        if (xIndex < 0 || rows.Count == 0)
        {
            return "category";
        }

        var values = rows
            .Select(row => row.ElementAtOrDefault(xIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (values.Count == 0)
        {
            return "category";
        }

        if (values.All(value => TryParseDate(value, out _)))
        {
            return "datetime";
        }

        if (values.All(value => TryParseNumber(value, out _)))
        {
            return "linear";
        }

        return "category";
    }

    private static string ResolveLabel(IReadOnlyList<string?> row, int xIndex, int groupIndex)
    {
        if (groupIndex >= 0)
        {
            return row.ElementAtOrDefault(groupIndex) ?? string.Empty;
        }

        return xIndex >= 0 ? row.ElementAtOrDefault(xIndex) ?? string.Empty : string.Empty;
    }

    private static object? ResolveXValue(string? raw, string scale)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (scale is "linear" or "logarithmic")
        {
            return TryParseNumber(raw, out var value) ? value : null;
        }

        if (scale == "datetime")
        {
            return TryParseDate(raw, out var date) ? date.ToString("o") : raw;
        }

        return raw;
    }

    private static double? ReadNumber(IReadOnlyList<string?> row, int index)
    {
        if (index < 0 || index >= row.Count)
        {
            return null;
        }

        var raw = row[index];
        return TryParseNumber(raw, out var value) ? value : null;
    }

    private static bool TryParseNumber(string? value, out double number)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            number = 0;
            return false;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number) ||
               double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out number);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = DateTimeOffset.MinValue;
            return false;
        }

        return DateTimeOffset.TryParse(value, out date);
    }

    private static string BuildSeriesKey(SeriesDefinition definition, int groupIndex, ChartVisualizationConfig config, string? groupBy)
    {
        if (groupIndex >= 0 && !string.IsNullOrWhiteSpace(definition.GroupValue) && !string.IsNullOrWhiteSpace(groupBy))
        {
            return $"{definition.YColumn}|{groupBy}={definition.GroupValue}";
        }

        return definition.YColumn;
    }

    private sealed class SeriesDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string YColumn { get; set; } = string.Empty;
        public string? GroupValue { get; set; }
        public string Axis { get; set; } = "left";
        public string Type { get; set; } = "bar";
        public int? ZIndex { get; set; }
        public int YIndex { get; set; }

        public ChartSeriesRenderModel ToSeriesRenderModel()
        {
            return new ChartSeriesRenderModel
            {
                Key = Key,
                Label = Label,
                Axis = Axis,
                Type = Type,
                ZIndex = ZIndex,
                Data = new List<object?>()
            };
        }
    }
}
