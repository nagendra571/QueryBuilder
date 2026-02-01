using System.Globalization;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public class CounterVisualizationDataBuilder
{
    public CounterVisualizationRenderModel Build(
        CounterVisualizationConfig? config,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var normalizedConfig = config ?? CounterVisualizationConfig.CreateDefault();
        NormalizeConfig(normalizedConfig);

        var render = new CounterVisualizationRenderModel
        {
            Label = normalizedConfig.General.Label,
            RowCount = rows?.Count ?? 0
        };

        if ((columns == null || columns.Count == 0) && !normalizedConfig.General.CountRows)
        {
            render.Errors.Add("No columns available.");
            render.Success = false;
            return render;
        }

        var columnLookup = (columns ?? Array.Empty<string>())
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        if (normalizedConfig.General.CountRows)
        {
            var countValue = rows?.Count ?? 0;
            render.ValueNumber = countValue;
            render.ValueRaw = countValue.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(normalizedConfig.General.ValueColumn))
            {
                render.Errors.Add("Counter value column is required.");
            }
            else if (!columnLookup.TryGetValue(normalizedConfig.General.ValueColumn, out var valueIndex))
            {
                render.Errors.Add($"Column '{normalizedConfig.General.ValueColumn}' not found.");
            }
            else
            {
                var rowIndex = normalizedConfig.General.ValueRow - 1;
                if (rowIndex < 0)
                {
                    render.Errors.Add("Counter value row must be at least 1.");
                }
                else if (rows == null || rowIndex >= rows.Count)
                {
                    render.Errors.Add($"Row {normalizedConfig.General.ValueRow} not available.");
                }
                else
                {
                    var valueRow = rows[rowIndex];
                    render.ValueRaw = valueIndex < valueRow.Count ? valueRow[valueIndex] : null;
                    render.ValueNumber = TryParseNumber(render.ValueRaw);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedConfig.General.TargetColumn))
        {
            if (!columnLookup.TryGetValue(normalizedConfig.General.TargetColumn, out var targetIndex))
            {
                render.Warnings.Add($"Target column '{normalizedConfig.General.TargetColumn}' not found.");
            }
            else
            {
                var targetRowIndex = normalizedConfig.General.TargetRow - 1;
                if (targetRowIndex < 0)
                {
                    render.Warnings.Add("Target row must be at least 1.");
                }
                else if (rows == null || targetRowIndex >= rows.Count)
                {
                    render.Warnings.Add($"Target row {normalizedConfig.General.TargetRow} not available.");
                }
                else
                {
                    var targetRow = rows[targetRowIndex];
                    render.TargetRaw = targetIndex < targetRow.Count ? targetRow[targetIndex] : null;
                    render.TargetNumber = TryParseNumber(render.TargetRaw);
                }
            }
        }

        render.Success = render.Errors.Count == 0;
        return render;
    }

    private static void NormalizeConfig(CounterVisualizationConfig config)
    {
        config.Type = "counter";
        config.General ??= new CounterGeneralConfig();
        config.Format ??= new CounterFormatConfig();
        if (config.General.ValueRow <= 0)
        {
            config.General.ValueRow = 1;
        }
        if (config.General.TargetRow <= 0)
        {
            config.General.TargetRow = 1;
        }
        config.Format.NumberFormat = string.IsNullOrWhiteSpace(config.Format.NumberFormat)
            ? "0,0"
            : config.Format.NumberFormat;
        config.Format.PositiveColor = string.IsNullOrWhiteSpace(config.Format.PositiveColor)
            ? "green"
            : config.Format.PositiveColor;
        config.Format.NegativeColor = string.IsNullOrWhiteSpace(config.Format.NegativeColor)
            ? "red"
            : config.Format.NegativeColor;
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
