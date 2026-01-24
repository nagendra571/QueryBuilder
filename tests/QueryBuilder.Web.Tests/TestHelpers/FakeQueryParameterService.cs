using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.TestHelpers;

public class FakeQueryParameterService : IQueryParameterService
{
    private readonly QueryParameterApplyResult _result;

    public FakeQueryParameterService(QueryParameterApplyResult? result = null)
    {
        _result = result ?? new QueryParameterApplyResult
        {
            Success = true,
            AppliedValues = new Dictionary<string, string?>()
        };
    }

    public Task<QueryParameterApplyResult> ApplyAsync(QueryParameterApplyRequest request)
    {
        if (string.IsNullOrWhiteSpace(_result.Sql))
        {
            _result.Sql = request.Sql;
        }
        return Task.FromResult(_result);
    }
}
