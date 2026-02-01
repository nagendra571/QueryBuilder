using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Models.Admin;
using QueryBuilder.Web.Options;

namespace QueryBuilder.Web.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private const string CachePrefix = "FeatureFlags:";
    private const string CacheListKey = "FeatureFlags:__all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

    private readonly ApplicationDbContext _dbContext;
    private readonly FeatureOptions _defaultOptions;
    private readonly IMemoryCache _cache;

    private static readonly IReadOnlyDictionary<string, string> KnownFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NewAppearance"] = "Enable compact enterprise UI."
    };

    public FeatureFlagService(ApplicationDbContext dbContext, IOptions<FeatureOptions> options, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _defaultOptions = options.Value ?? new FeatureOptions();
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var cacheKey = $"{CachePrefix}{key}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        try
        {
            var stored = await _dbContext.FeatureFlags.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Key == key);
            if (stored != null)
            {
                _cache.Set(cacheKey, stored.IsEnabled, CacheTtl);
                return stored.IsEnabled;
            }
        }
        catch
        {
            return GetDefaultValue(key);
        }

        var fallback = GetDefaultValue(key);
        _cache.Set(cacheKey, fallback, CacheTtl);
        return fallback;
    }

    public async Task<IReadOnlyList<FeatureFlagItemViewModel>> GetAllAsync()
    {
        if (_cache.TryGetValue(CacheListKey, out IReadOnlyList<FeatureFlagItemViewModel> cached))
        {
            return cached;
        }

        List<FeatureFlag> stored;
        try
        {
            stored = await _dbContext.FeatureFlags.AsNoTracking().ToListAsync();
        }
        catch
        {
            stored = new List<FeatureFlag>();
        }

        var storedMap = stored.ToDictionary(f => f.Key, f => f, StringComparer.OrdinalIgnoreCase);
        var flags = new List<FeatureFlagItemViewModel>();

        foreach (var kvp in KnownFlags)
        {
            var item = BuildViewModel(kvp.Key, kvp.Value, storedMap.TryGetValue(kvp.Key, out var storedFlag) ? storedFlag : null);
            flags.Add(item);
        }

        foreach (var storedFlag in stored)
        {
            if (KnownFlags.ContainsKey(storedFlag.Key))
            {
                continue;
            }

            flags.Add(BuildViewModel(storedFlag.Key, storedFlag.Description, storedFlag));
        }

        var ordered = flags.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase).ToList();
        _cache.Set(CacheListKey, ordered, CacheTtl);
        return ordered;
    }

    public async Task UpdateAsync(string key, bool isEnabled, string updatedBy, byte[]? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var normalizedKey = key.Trim();
        if (normalizedKey.Length > 100)
        {
            throw new ArgumentException("Key must be 100 characters or fewer.", nameof(key));
        }

        var entity = await _dbContext.FeatureFlags.FirstOrDefaultAsync(f => f.Key == normalizedKey);
        if (entity == null)
        {
            entity = new FeatureFlag
            {
                Key = normalizedKey,
                Description = KnownFlags.TryGetValue(normalizedKey, out var description) ? description : null
            };
            _dbContext.FeatureFlags.Add(entity);
        }

        if (rowVersion != null && entity.RowVersion != null && !rowVersion.SequenceEqual(entity.RowVersion))
        {
            throw new FeatureFlagConcurrencyException(BuildConcurrencyMessage(entity));
        }

        if (rowVersion != null)
        {
            _dbContext.Entry(entity).Property(e => e.RowVersion).OriginalValue = rowVersion;
        }

        entity.IsEnabled = isEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedBy;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            var latest = await _dbContext.FeatureFlags.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Key == key);
            throw new FeatureFlagConcurrencyException(BuildConcurrencyMessage(latest));
        }

        InvalidateCache(normalizedKey);
    }

    public async Task CreateAsync(string key, string? description, bool isEnabled, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var normalizedKey = key.Trim();
        if (normalizedKey.Length > 100)
        {
            throw new ArgumentException("Key must be 100 characters or fewer.", nameof(key));
        }
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > 500)
        {
            throw new ArgumentException("Description must be 500 characters or fewer.", nameof(description));
        }
        var exists = await _dbContext.FeatureFlags.AnyAsync(f => f.Key == normalizedKey);
        if (exists)
        {
            throw new InvalidOperationException("Feature flag key already exists.");
        }

        var entity = new FeatureFlag
        {
            Key = normalizedKey,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsEnabled = isEnabled,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        _dbContext.FeatureFlags.Add(entity);
        await _dbContext.SaveChangesAsync();

        InvalidateCache(normalizedKey);
    }

    private FeatureFlagItemViewModel BuildViewModel(string key, string? description, FeatureFlag? storedFlag)
    {
        if (storedFlag == null)
        {
            return new FeatureFlagItemViewModel
            {
                Key = key,
                Description = description,
                IsEnabled = GetDefaultValue(key),
                UpdatedAt = null,
                UpdatedBy = null,
                RowVersion = null
            };
        }

        return new FeatureFlagItemViewModel
        {
            Key = storedFlag.Key,
            Description = storedFlag.Description ?? description,
            IsEnabled = storedFlag.IsEnabled,
            UpdatedAt = storedFlag.UpdatedAt,
            UpdatedBy = storedFlag.UpdatedBy,
            RowVersion = storedFlag.RowVersion == null ? null : Convert.ToBase64String(storedFlag.RowVersion)
        };
    }

    private static string BuildConcurrencyMessage(FeatureFlag? current)
    {
        if (current == null)
        {
            return "This flag was updated by another user. Refresh and try again.";
        }

        var updatedBy = string.IsNullOrWhiteSpace(current.UpdatedBy) ? "another user" : current.UpdatedBy;
        var updatedAt = current.UpdatedAt == default ? "recently" : current.UpdatedAt.ToLocalTime().ToString("g");
        return $"This flag was updated by {updatedBy} at {updatedAt}. Refresh and try again.";
    }

    private bool GetDefaultValue(string key)
    {
        return key switch
        {
            "NewAppearance" => _defaultOptions.NewAppearance,
            _ => false
        };
    }

    private void InvalidateCache(string key)
    {
        _cache.Remove($"{CachePrefix}{key}");
        _cache.Remove(CacheListKey);
    }
}
