using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.Services;

public class ChartVisualizationDataBuilderTests
{
    [Fact]
    public void Build_Creates_Series_Per_Group()
    {
        var builder = new ChartVisualizationDataBuilder();
        var config = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "date",
                YColumns = new List<string> { "sales" },
                GroupBy = "region"
            }
        };
        var columns = new[] { "date", "region", "sales" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "2024-01-01", "East", "10" },
            new[] { "2024-01-01", "West", "5" }
        };

        var render = builder.Build(config, columns, rows);

        render.Series.Should().HaveCount(2);
        render.Series.Select(s => s.Key).Should().Contain(new[] { "sales|region=East", "sales|region=West" });
    }

    [Fact]
    public void Build_Normalizes_To_Percent()
    {
        var builder = new ChartVisualizationDataBuilder();
        var config = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "x",
                YColumns = new List<string> { "a", "b" },
                NormalizeToPercent = true
            }
        };
        var columns = new[] { "x", "a", "b" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "1", "2", "2" }
        };

        var render = builder.Build(config, columns, rows);

        render.Series.Should().HaveCount(2);
        render.Series.All(s => s.Data.Count == 1).Should().BeTrue();
        render.Series.Select(s => s.Data[0]).Should().BeEquivalentTo(new object?[] { 50d, 50d });
    }

    [Fact]
    public void Build_Respects_Null_As_Zero()
    {
        var builder = new ChartVisualizationDataBuilder();
        var columns = new[] { "x", "a" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "1", null }
        };

        var configZero = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "x",
                YColumns = new List<string> { "a" },
                NullAsZero = true
            }
        };
        var renderZero = builder.Build(configZero, columns, rows);
        renderZero.Series[0].Data[0].Should().Be(0d);

        var configNull = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "x",
                YColumns = new List<string> { "a" },
                NullAsZero = false
            }
        };
        var renderNull = builder.Build(configNull, columns, rows);
        renderNull.Series[0].Data[0].Should().BeNull();
    }

    [Fact]
    public void Build_Sorts_And_Reverses_X_Axis()
    {
        var builder = new ChartVisualizationDataBuilder();
        var config = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "x",
                YColumns = new List<string> { "a" }
            },
            XAxis = new ChartXAxisConfig
            {
                SortValues = true,
                ReverseOrder = false
            }
        };
        var columns = new[] { "x", "a" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "2", "1" },
            new[] { "1", "2" },
            new[] { "3", "3" }
        };

        var render = builder.Build(config, columns, rows);
        render.Labels.Should().Equal(new[] { "1", "2", "3" });

        config.XAxis.ReverseOrder = true;
        var reversed = builder.Build(config, columns, rows);
        reversed.Labels.Should().Equal(new[] { "3", "2", "1" });
    }

    [Fact]
    public void Build_Stable_Series_Keys_With_Group_By_And_Multiple_Y()
    {
        var builder = new ChartVisualizationDataBuilder();
        var config = new ChartVisualizationConfig
        {
            General = new ChartGeneralConfig
            {
                ChartType = "bar",
                XColumn = "date",
                YColumns = new List<string> { "a", "b" },
                GroupBy = "region"
            }
        };
        var columns = new[] { "date", "region", "a", "b" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "2024-01-01", "East", "1", "2" }
        };

        var render = builder.Build(config, columns, rows);
        render.Series.Select(s => s.Key).Should().Contain(new[] { "a|region=East", "b|region=East" });
    }
}
