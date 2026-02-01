namespace QueryBuilder.Web.Services;

public class FeatureFlagConcurrencyException : Exception
{
    public FeatureFlagConcurrencyException(string message) : base(message)
    {
    }
}
