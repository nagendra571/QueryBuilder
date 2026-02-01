using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Tests.Services;

public class ChartVisualizationConfigSerializationTests
{
    [Fact]
    public void Chart_Config_Serializes_And_Deserializes()
    {
        var config = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "line",
                XColumn = "date",
                YColumns = new List<string> { "sales" },
                GroupBy = "region",
                ShowLegend = false,
                Stacking = "stack",
                NormalizeToPercent = true,
                NullAsZero = false
            },
            XAxis = new ChartXAxisConfig
            {
                Scale = "datetime",
                Name = "Order Date",
                SortValues = true,
                ReverseOrder = true,
                ShowLabels = false
            },
            YAxis = new ChartYAxisConfig
            {
                Left = new ChartAxisSettings { Scale = "linear", Name = "Sales", Min = 0, Max = 100, Reverse = false },
                Right = new ChartAxisSettings { Scale = "linear", Name = "Alt", Min = null, Max = null, Reverse = true }
            },
            Series = new List<ChartSeriesConfig>
            {
                new() { Key = "sales|region=East", Label = "East Sales", Axis = "left", Type = "bar", ZIndex = 1 }
            },
            Colors = new Dictionary<string, string> { ["sales|region=East"] = "blue" },
            DataLabels = new ChartDataLabelsConfig
            {
                Enabled = true,
                NumberFormat = "0,0.00",
                PercentFormat = "0[.]00%",
                DateTimeFormat = "DD/MM/YY HH:mm",
                LabelTemplate = "auto"
            }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(config, options);
        var roundTrip = JsonSerializer.Deserialize<ChartVisualizationConfig>(json, options);

        roundTrip.Should().NotBeNull();
        roundTrip!.General.ChartType.Should().Be("line");
        roundTrip.General.YColumns.Should().ContainSingle("sales");
        roundTrip.Series.Should().ContainSingle(s => s.Key == "sales|region=East");
        roundTrip.Colors.Should().ContainKey("sales|region=East");
    }
}
