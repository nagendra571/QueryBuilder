using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;

namespace QueryBuilder.Web.Services;

public class QueryParameterService : IQueryParameterService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QueryRunner _queryRunner;

    public QueryParameterService(ApplicationDbContext dbContext, QueryRunner queryRunner)
    {
        _dbContext = dbContext;
        _queryRunner = queryRunner;
    }

    public async Task<QueryParameterApplyResult> ApplyAsync(QueryParameterApplyRequest request)
    {
        var result = new QueryParameterApplyResult
        {
            Sql = request.Sql
        };

        var tokens = QueryParameterParser.ExtractTokens(request.Sql);
        if (tokens.Count == 0)
        {
            result.Success = true;
            return result;
        }

        var definitions = request.Definitions
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var (baseName, suffix) = SplitToken(token);
            if (!definitions.TryGetValue(baseName, out var definition))
            {
                result.Errors.Add($"Unknown parameter '{baseName}'.");
                continue;
            }

            if (suffix != null && !IsRangeToken(definition.Type))
            {
                result.Errors.Add($"Parameter '{baseName}' does not support '{suffix}'.");
                continue;
            }

            var valueKey = suffix == null ? baseName : $"{baseName}.{suffix}";
            var value = request.Values.TryGetValue(valueKey, out var providedValue)
                ? providedValue
                : definition.DefaultValue;

            if (string.IsNullOrWhiteSpace(value))
            {
                if (definition.IsRequired)
                {
                    result.Errors.Add($"Parameter '{baseName}' is required.");
                }
                continue;
            }

            var formatted = await FormatValueAsync(definition, value!, suffix, request.AllowText, result.Errors);
            if (formatted != null)
            {
                replacements[token] = formatted;
                result.AppliedValues[valueKey] = value;
            }
        }

        if (result.Errors.Count > 0)
        {
            result.Success = false;
            return result;
        }

        var substituted = Substitute(request.Sql, replacements);
        if (!IsSafeSelect(substituted))
        {
            result.Errors.Add("Query must be a single SELECT statement.");
            result.Success = false;
            return result;
        }

        result.Sql = substituted;
        result.Success = true;
        return result;
    }

    private static string Substitute(string sql, Dictionary<string, string> replacements)
    {
        if (replacements.Count == 0)
        {
            return sql;
        }

        return QueryParameterParser.TokenRegex.Replace(sql, match =>
        {
            var token = match.Groups[1].Value.Trim();
            return replacements.TryGetValue(token, out var value) ? value : match.Value;
        });
    }

    private static (string baseName, string? suffix) SplitToken(string token)
    {
        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (token, null);
    }

    private static bool IsRangeToken(QueryParameterType type)
    {
        return type == QueryParameterType.DateRange || type == QueryParameterType.DateTimeRange;
    }

    private async Task<string?> FormatValueAsync(
        QueryParameterDefinition definition,
        string value,
        string? suffix,
        bool allowText,
        List<string> errors)
    {
        switch (definition.Type)
        {
            case QueryParameterType.Text:
                if (!allowText)
                {
                    errors.Add($"Text parameter '{definition.Name}' is not allowed.");
                    return null;
                }
                return $"'{EscapeSql(value)}'";
            case QueryParameterType.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    errors.Add($"Parameter '{definition.Name}' must be a number.");
                    return null;
                }
                return number.ToString(CultureInfo.InvariantCulture);
            case QueryParameterType.Dropdown:
                var options = await ResolveDropdownOptionsAsync(definition);
                if (options.Count > 0 && !options.Contains(value))
                {
                    errors.Add($"Parameter '{definition.Name}' has an invalid value.");
                    return null;
                }
                if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
                {
                    return numericValue.ToString(CultureInfo.InvariantCulture);
                }
                return $"'{EscapeSql(value)}'";
            case QueryParameterType.Date:
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateValue))
                {
                    errors.Add($"Parameter '{definition.Name}' must be a valid date.");
                    return null;
                }
                return $"'{dateValue:yyyy-MM-dd}'";
            case QueryParameterType.DateTime:
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTimeValue))
                {
                    errors.Add($"Parameter '{definition.Name}' must be a valid datetime.");
                    return null;
                }
                return $"'{dateTimeValue:yyyy-MM-dd HH:mm:ss}'";
            case QueryParameterType.DateRange:
            case QueryParameterType.DateTimeRange:
                if (suffix is not ("start" or "end"))
                {
                    errors.Add($"Parameter '{definition.Name}' requires start and end values.");
                    return null;
                }
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var rangeValue))
                {
                    errors.Add($"Parameter '{definition.Name}.{suffix}' must be a valid date.");
                    return null;
                }
                var format = definition.Type == QueryParameterType.DateRange ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss";
                return $"'{rangeValue.ToString(format, CultureInfo.InvariantCulture)}'";
            default:
                errors.Add($"Parameter '{definition.Name}' has unsupported type.");
                return null;
        }
    }

    private async Task<HashSet<string>> ResolveDropdownOptionsAsync(QueryParameterDefinition definition)
    {
        var options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(definition.SettingsJson))
        {
            return options;
        }

        using var document = JsonDocument.Parse(definition.SettingsJson);
        if (document.RootElement.TryGetProperty("options", out var optionsElement) &&
            optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in optionsElement.EnumerateArray())
            {
                if (option.TryGetProperty("value", out var valueElement))
                {
                    var value = valueElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        options.Add(value);
                    }
                }
            }
            return options;
        }

        if (document.RootElement.TryGetProperty("sourceQueryId", out var queryIdElement) &&
            queryIdElement.TryGetInt32(out var queryId))
        {
            var query = await _dbContext.Queries.FirstOrDefaultAsync(q => q.Id == queryId);
            if (query == null)
            {
                return options;
            }

            var result = await _queryRunner.RunAsync(query.DataSourceId, query.SqlText);
            if (!result.Success || result.Rows.Count == 0)
            {
                return options;
            }

            foreach (var row in result.Rows)
            {
                if (row.Count == 0)
                {
                    continue;
                }

                var value = row.Count > 1 ? row[1] : row[0];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    options.Add(value);
                }
            }
        }

        return options;
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    private static bool IsSafeSelect(string sql)
    {
        var trimmed = sql.Trim();
        if (!(trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var withoutTrailing = trimmed.TrimEnd().TrimEnd(';');
        return !withoutTrailing.Contains(';');
    }
}
