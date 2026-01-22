using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;

namespace QueryBuilder.Web.Tests.TestHelpers;

public class FakeQueryParameterService : IQueryParameterService
{
    public Task<QueryParameterApplyResult> ApplyAsync(QueryParameterApplyRequest request)
    {
        return Task.FromResult(new QueryParameterApplyResult
        {
            Success = true,
            Sql = request.Sql,
            AppliedValues = new Dictionary<string, string?>()
        });
    }
}
