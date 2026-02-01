using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Tests.Services;

public class CounterVisualizationConfigSerializationTests
{
    [Fact]
    public void Counter_Config_RoundTrips_With_CamelCase()
    {
        var config = new CounterVisualizationConfig
        {
            General = new CounterGeneralConfig
            {
                Label = "Total Sales",
                CountRows = false,
                ValueColumn = "sales",
                ValueRow = 2,
                TargetColumn = "target",
                TargetRow = 1
            },
            Format = new CounterFormatConfig
            {
                NumberFormat = "0,0.00",
                ShowTarget = true,
                Prefix = "$",
                Suffix = "",
                PositiveColor = "green",
                NegativeColor = "red"
            }
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var json = JsonSerializer.Serialize(config, options);
        var roundTrip = JsonSerializer.Deserialize<CounterVisualizationConfig>(json, options);

        roundTrip.Should().NotBeNull();
        roundTrip!.General.Label.Should().Be("Total Sales");
        roundTrip.General.ValueRow.Should().Be(2);
        roundTrip.Format.NumberFormat.Should().Be("0,0.00");
        roundTrip.Format.Prefix.Should().Be("$");
    }
}
