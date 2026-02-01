using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;
using System.Text.RegularExpressions;

namespace QueryBuilder.Web.Tests.Services;

public class FunnelVisualizationDataBuilderTests
{
    [Fact]
    public void Build_Computes_Percentages()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "100" },
            new[] { "B", "50" }
        };

        var render = builder.Build(config, columns, rows);

        render.Success.Should().BeTrue();
        render.Rows.Should().HaveCount(2);
        render.Rows[0].PercentMax.Should().BeApproximately(100d, 0.01);
        render.Rows[1].PercentMax.Should().BeApproximately(50d, 0.01);
        render.Rows[0].PercentPrevious.Should().Be(100d);
        render.Rows[1].PercentPrevious.Should().BeApproximately(50d, 0.01);
    }

    [Fact]
    public void Build_Sorts_By_Selected_Column_When_AutoSort_Enabled()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = true,
            SortByColumn = "value"
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "10" },
            new[] { "B", "30" },
            new[] { "C", "20" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.Select(r => r.StepLabel).Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public void Build_Treats_Null_As_Zero()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", null },
            new[] { "B", "5" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows[0].Value.Should().Be(0);
    }

    [Fact]
    public void Build_Handles_Max_Zero()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value"
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "0" },
            new[] { "B", "0" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.All(r => r.PercentMax == 0).Should().BeTrue();
    }

    [Fact]
    public void Build_Aggregates_Duplicate_Steps_With_Sum_By_Default()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false,
            Format = new FunnelFormatConfig
            {
                AggregateSteps = true,
                Aggregation = "sum"
            }
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "10" },
            new[] { "A", "5" },
            new[] { "B", "3" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.Should().HaveCount(2);
        render.Rows[0].StepLabel.Should().Be("A");
        render.Rows[0].Value.Should().Be(15);
    }

    [Theory]
    [InlineData("avg", 7.5)]
    [InlineData("min", 5)]
    [InlineData("max", 10)]
    [InlineData("count", 2)]
    public void Build_Aggregates_Using_Selected_Function(string aggregation, double expected)
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false,
            Format = new FunnelFormatConfig
            {
                AggregateSteps = true,
                Aggregation = aggregation
            }
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "10" },
            new[] { "A", "5" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.Should().HaveCount(1);
        render.Rows[0].Value.Should().Be(expected);
    }

    [Fact]
    public void Build_Skips_Nulls_When_Configured()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false,
            Format = new FunnelFormatConfig
            {
                NullHandling = "skip"
            }
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", null },
            new[] { "B", "5" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.Should().HaveCount(1);
        render.Rows[0].StepLabel.Should().Be("B");
    }

    [Fact]
    public void Build_Applies_TopN_And_Others_Row()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = true,
            Format = new FunnelFormatConfig
            {
                TopN = 1,
                IncludeOthers = true,
                AggregateSteps = false
            }
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "10" },
            new[] { "B", "6" },
            new[] { "C", "4" }
        };

        var render = builder.Build(config, columns, rows);

        render.Rows.Should().HaveCount(2);
        render.Rows[0].StepLabel.Should().Be("A");
        render.Rows[1].StepLabel.Should().Be("Others");
        render.Rows[1].Value.Should().Be(10);
    }

    [Fact]
    public void Build_Formats_Percent_Text_With_Precision()
    {
        var builder = new FunnelVisualizationDataBuilder();
        var config = new FunnelVisualizationConfig
        {
            StepColumn = "step",
            ValueColumn = "value",
            AutoSort = false,
            Format = new FunnelFormatConfig
            {
                PercentPrecision = 1,
                ShowPercentSign = true
            }
        };
        var columns = new[] { "step", "value" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "A", "10" },
            new[] { "B", "5" }
        };

        var render = builder.Build(config, columns, rows);

        var percentText = render.Rows[0].PercentMaxText ?? string.Empty;
        var regex = new Regex(@"^\d+[\.,]\d{1}%$");
        regex.IsMatch(percentText).Should().BeTrue();
    }
}
