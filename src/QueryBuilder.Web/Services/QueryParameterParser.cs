using System.Text.RegularExpressions;

namespace QueryBuilder.Web.Services;

public static class QueryParameterParser
{
    public static readonly Regex TokenRegex = new(@"\{\{\s*([a-zA-Z0-9_\.]+)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractTokens(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Array.Empty<string>();
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TokenRegex.Matches(sql))
        {
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            var token = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        return tokens.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
