using FluentAssertions;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.Services;

public class QueryParameterParserTests
{
    [Fact]
    public void ExtractTokens_IgnoresWhitespace_And_Dedupes()
    {
        var sql = "select {{ foo }} , {{Foo}} , {{bar}}";

        var tokens = QueryParameterParser.ExtractTokens(sql);

        tokens.Should().HaveCount(2);
        tokens.Should().Contain(t => t.Equals("foo", StringComparison.OrdinalIgnoreCase));
        tokens.Should().Contain(t => t.Equals("bar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractTokens_Captures_Range_Tokens()
    {
        var sql = "where {{dr.start}} and {{dr.end}}";

        var tokens = QueryParameterParser.ExtractTokens(sql);

        tokens.Should().Contain(t => t.Equals("dr.start", StringComparison.OrdinalIgnoreCase));
        tokens.Should().Contain(t => t.Equals("dr.end", StringComparison.OrdinalIgnoreCase));
    }
}
