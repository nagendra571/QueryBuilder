using QueryBuilder.Web.Models.Admin;

namespace QueryBuilder.Web.Services;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key);
    Task<IReadOnlyList<FeatureFlagItemViewModel>> GetAllAsync();
    Task UpdateAsync(string key, bool isEnabled, string updatedBy, byte[]? rowVersion);
    Task CreateAsync(string key, string? description, bool isEnabled, string updatedBy);
}
