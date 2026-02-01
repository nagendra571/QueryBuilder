using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

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
}
