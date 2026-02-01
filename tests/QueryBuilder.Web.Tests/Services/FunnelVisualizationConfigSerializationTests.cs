using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Tests.Services;

public class FunnelVisualizationConfigSerializationTests
{
    [Fact]
    public void Funnel_Config_RoundTrips_With_CamelCase()
    {
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "stage",
            StepDisplayName = "Steps",
            ValueColumn = "count",
            ValueDisplayName = "Value",
            AutoSort = true,
            SortByColumn = "count",
            SortDirection = "desc",
            TreatNullAsZero = true
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var json = JsonSerializer.Serialize(config, options);
        var roundTrip = JsonSerializer.Deserialize<FunnelVisualizationConfig>(json, options);

        roundTrip.Should().NotBeNull();
        roundTrip!.StepColumn.Should().Be("stage");
        roundTrip.ValueColumn.Should().Be("count");
        roundTrip.AutoSort.Should().BeTrue();
        roundTrip.SortDirection.Should().Be("desc");
    }

    [Fact]
    public void Funnel_Config_Applies_Format_Defaults_When_Missing()
    {
        var json = "{\"type\":\"funnel\",\"stepColumn\":\"stage\",\"valueColumn\":\"count\"}";
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var roundTrip = JsonSerializer.Deserialize<FunnelVisualizationConfig>(json, options);

        roundTrip.Should().NotBeNull();
        roundTrip!.Format.Should().NotBeNull();
        roundTrip.Format.ValueBarColor.Should().NotBeNullOrWhiteSpace();
        roundTrip.Format.PercentPrecision.Should().Be(2);
        roundTrip.Format.Aggregation.Should().Be("sum");
    }
}
