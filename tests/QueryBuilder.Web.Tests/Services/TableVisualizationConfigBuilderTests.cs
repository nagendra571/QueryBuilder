using System.Text.Json;
using FluentAssertions;
using QueryBuilder.Web.Models.Visualizations;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.Services;

public class TableVisualizationConfigBuilderTests
{
    [Fact]
    public void Build_Infers_DisplayTypes_From_Rows()
    {
        var builder = new TableVisualizationConfigBuilder();
        var columns = new[] { "id", "created", "active", "name" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "1", "2024-01-02 10:30:00", "true", "Alice" },
            new string?[] { "2", "2024-01-03 11:45:00", "false", "Bob" }
        };

        var config = builder.Build(columns, rows);

        config.Columns["id"].DisplayAs.Should().Be("number");
        config.Columns["created"].DisplayAs.Should().Be("datetime");
        config.Columns["active"].DisplayAs.Should().Be("boolean");
        config.Columns["name"].DisplayAs.Should().Be("text");
        config.Columns["id"].Alignment.Should().Be("right");
    }

    [Fact]
    public void Build_Preserves_Existing_Column_Config()
    {
        var builder = new TableVisualizationConfigBuilder();
        var existing = new TableVisualizationConfig
        {
            Grid = new TableGridConfig { PageSize = 10 },
            Columns = new Dictionary<string, TableColumnConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = new TableColumnConfig
                {
                    DisplayAs = "link",
                    UrlTemplate = "https://example.com/{{value}}",
                    Alignment = "left"
                }
            }
        };
        var columns = new[] { "name", "score" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "Alice", "95" }
        };

        var config = builder.Build(columns, rows, existing);

        config.Grid.PageSize.Should().Be(10);
        config.Columns["name"].DisplayAs.Should().Be("link");
        config.Columns["name"].UrlTemplate.Should().Be("https://example.com/{{value}}");
        config.Columns["score"].DisplayAs.Should().Be("number");
    }

    [Fact]
    public void Config_RoundTrips_With_CamelCase_Json()
    {
        var builder = new TableVisualizationConfigBuilder();
        var columns = new[] { "id" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "42" }
        };

        var config = builder.Build(columns, rows);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var json = JsonSerializer.Serialize(config, options);
        var roundTrip = JsonSerializer.Deserialize<TableVisualizationConfig>(json, options);

        roundTrip.Should().NotBeNull();
        roundTrip!.Columns["id"].DisplayAs.Should().Be("number");
    }

    [Fact]
    public void Build_Defaults_Visibility_And_Search_Flags()
    {
        var builder = new TableVisualizationConfigBuilder();
        var columns = new[] { "id", "name" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new string?[] { "1", "Alice" }
        };

        var config = builder.Build(columns, rows);

        config.Columns["id"].IsVisible.Should().BeTrue();
        config.Columns["name"].IsVisible.Should().BeTrue();
        config.Columns["id"].UseForSearch.Should().BeFalse();
        config.Columns["name"].UseForSearch.Should().BeTrue();
    }
}
