using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.Services;

public class CounterVisualizationDataBuilderTests
{
    [Fact]
    public void Build_Uses_OneBased_Row_Index_For_Value()
    {
        var builder = new CounterVisualizationDataBuilder();
        var config = new CounterVisualizationConfig
        {
            General = new CounterGeneralConfig
            {
                ValueColumn = "sales",
                ValueRow = 2
            }
        };
        var columns = new[] { "sales" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "10" },
            new string?[] { "20" }
        };

        var render = builder.Build(config, columns, rows);

        render.Success.Should().BeTrue();
        render.ValueNumber.Should().Be(20);
    }

    [Fact]
    public void Build_Uses_Row_Count_When_CountRows_Enabled()
    {
        var builder = new CounterVisualizationDataBuilder();
        var config = new CounterVisualizationConfig
        {
            General = new CounterGeneralConfig
            {
                CountRows = true
            }
        };
        var columns = new[] { "id" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "1" },
            new string?[] { "2" },
            new string?[] { "3" }
        };

        var render = builder.Build(config, columns, rows);

        render.Success.Should().BeTrue();
        render.ValueNumber.Should().Be(3);
    }

    [Fact]
    public void Build_Returns_Error_When_Value_Column_Missing()
    {
        var builder = new CounterVisualizationDataBuilder();
        var config = new CounterVisualizationConfig
        {
            General = new CounterGeneralConfig
            {
                ValueColumn = "missing",
                ValueRow = 1
            }
        };
        var columns = new[] { "sales" };
        var rows = new List<IReadOnlyList<string?>> { new string?[] { "10" } };

        var render = builder.Build(config, columns, rows);

        render.Success.Should().BeFalse();
        render.Errors.Should().Contain(e => e.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_Warns_When_Target_Row_Is_Invalid()
    {
        var builder = new CounterVisualizationDataBuilder();
        var config = new CounterVisualizationConfig
        {
            General = new CounterGeneralConfig
            {
                ValueColumn = "sales",
                ValueRow = 1,
                TargetColumn = "target",
                TargetRow = 3
            }
        };
        var columns = new[] { "sales", "target" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "10", "12" }
        };

        var render = builder.Build(config, columns, rows);

        render.Success.Should().BeTrue();
        render.Warnings.Should().NotBeEmpty();
        render.TargetNumber.Should().BeNull();
    }
}
