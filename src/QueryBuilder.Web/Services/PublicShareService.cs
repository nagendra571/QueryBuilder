using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Data;

namespace QueryBuilder.Web.Services;

public class PublicShareService
{
    private readonly ApplicationDbContext _dbContext;

    public PublicShareService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PublicShare?> GetActiveShareAsync(PublicShareEntityType entityType, int entityId)
    {
        return await _dbContext.PublicShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);
    }

    public async Task<PublicShare?> GetValidShareByTokenAsync(PublicShareEntityType entityType, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var share = await _dbContext.PublicShares
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.Token == token);

        if (share == null || !share.IsEnabled)
        {
            return null;
        }

        if (share.ExpiresAt.HasValue && share.ExpiresAt.Value <= now)
        {
            return null;
        }

        share.LastAccessedAt = now;
        await _dbContext.SaveChangesAsync();
        return share;
    }

    public async Task<PublicShare> EnableAsync(PublicShareEntityType entityType, int entityId, string createdByUserId, DateTimeOffset? expiresAt)
    {
        var share = await _dbContext.PublicShares.FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);
        if (share == null)
        {
            share = new PublicShare
            {
                EntityType = entityType,
                EntityId = entityId,
                AccessLevel = ShareAccessLevel.View,
                Token = await GenerateUniqueTokenAsync(),
                IsEnabled = true,
                ExpiresAt = expiresAt,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = createdByUserId
            };
            _dbContext.PublicShares.Add(share);
        }
        else
        {
            share.AccessLevel = ShareAccessLevel.View;
            share.IsEnabled = true;
            share.ExpiresAt = expiresAt;
            if (string.IsNullOrWhiteSpace(share.Token))
            {
                share.Token = await GenerateUniqueTokenAsync();
            }
        }

        await _dbContext.SaveChangesAsync();
        return share;
    }

    public async Task<PublicShare> DisableAsync(PublicShareEntityType entityType, int entityId, string createdByUserId)
    {
        var share = await _dbContext.PublicShares.FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);
        if (share == null)
        {
            share = new PublicShare
            {
                EntityType = entityType,
                EntityId = entityId,
                AccessLevel = ShareAccessLevel.View,
                Token = await GenerateUniqueTokenAsync(),
                IsEnabled = false,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = createdByUserId
            };
            _dbContext.PublicShares.Add(share);
        }
        else
        {
            share.IsEnabled = false;
        }

        await _dbContext.SaveChangesAsync();
        return share;
    }

    public async Task<PublicShare> RegenerateAsync(PublicShareEntityType entityType, int entityId, string createdByUserId, DateTimeOffset? expiresAt)
    {
        var share = await _dbContext.PublicShares.FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);
        if (share == null)
        {
            share = new PublicShare
            {
                EntityType = entityType,
                EntityId = entityId,
                AccessLevel = ShareAccessLevel.View,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = createdByUserId
            };
            _dbContext.PublicShares.Add(share);
        }

        share.Token = await GenerateUniqueTokenAsync();
        share.IsEnabled = true;
        share.ExpiresAt = expiresAt;
        share.AccessLevel = ShareAccessLevel.View;
        await _dbContext.SaveChangesAsync();
        return share;
    }

    public async Task<PublicShare> SetExpirationAsync(PublicShareEntityType entityType, int entityId, DateTimeOffset? expiresAt, string createdByUserId)
    {
        var share = await _dbContext.PublicShares.FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);
        if (share == null)
        {
            share = new PublicShare
            {
                EntityType = entityType,
                EntityId = entityId,
                AccessLevel = ShareAccessLevel.View,
                Token = await GenerateUniqueTokenAsync(),
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = createdByUserId
            };
            _dbContext.PublicShares.Add(share);
        }

        share.ExpiresAt = expiresAt;
        await _dbContext.SaveChangesAsync();
        return share;
    }

    private async Task<string> GenerateUniqueTokenAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = GenerateToken();
            var exists = await _dbContext.PublicShares.AnyAsync(s => s.Token == token);
            if (!exists)
            {
                return token;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique public share token.");
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var base64 = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return base64;
    }
}
