using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Options;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class FeatureFlagServiceTests
{
    [Fact]
    public async Task IsEnabledAsync_UsesConfigFallback_WhenDbMissing()
    {
        using var db = TestDbContextFactory.CreateDbContext();
        var options = Microsoft.Extensions.Options.Options.Create(new FeatureOptions { NewAppearance = true });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(db, options, cache);

        var result = await service.IsEnabledAsync("NewAppearance");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_UsesDbValue_WhenPresent()
    {
        using var db = TestDbContextFactory.CreateDbContext();
        db.FeatureFlags.Add(new FeatureFlag
        {
            Key = "NewAppearance",
            IsEnabled = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FeatureOptions { NewAppearance = true });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(db, options, cache);

        var result = await service.IsEnabledAsync("NewAppearance");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCache()
    {
        using var db = TestDbContextFactory.CreateDbContext();
        var flag = new FeatureFlag
        {
            Key = "NewAppearance",
            IsEnabled = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "seed",
            RowVersion = new byte[] { 1 }
        };
        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FeatureOptions { NewAppearance = true });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(db, options, cache);

        (await service.IsEnabledAsync("NewAppearance")).Should().BeFalse();
        await service.UpdateAsync("NewAppearance", true, "admin", flag.RowVersion);

        (await service.IsEnabledAsync("NewAppearance")).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ThrowsOnConcurrencyMismatch()
    {
        using var db = TestDbContextFactory.CreateDbContext();
        var flag = new FeatureFlag
        {
            Key = "NewAppearance",
            IsEnabled = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "seed",
            RowVersion = new byte[] { 1 }
        };
        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FeatureOptions { NewAppearance = false });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(db, options, cache);

        var act = async () => await service.UpdateAsync("NewAppearance", true, "admin", new byte[] { 2 });

        await act.Should().ThrowAsync<FeatureFlagConcurrencyException>();
    }
}
