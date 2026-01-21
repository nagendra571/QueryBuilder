using FluentAssertions;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Services;

public class PublicShareServiceTests
{
    [Fact]
    public async Task EnableAsync_Creates_Token_And_Enables_Share()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        var service = new PublicShareService(dbContext);

        var share = await service.EnableAsync(PublicShareEntityType.Dashboard, 10, "user-1", null);

        share.IsEnabled.Should().BeTrue();
        share.Token.Should().NotBeNullOrWhiteSpace();
        share.AccessLevel.Should().Be(ShareAccessLevel.View);
    }

    [Fact]
    public async Task GetValidShareByTokenAsync_Returns_Null_When_Expired()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.PublicShares.Add(new PublicShare
        {
            EntityType = PublicShareEntityType.Dashboard,
            EntityId = 1,
            Token = "expired",
            IsEnabled = true,
            AccessLevel = ShareAccessLevel.View,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedByUserId = "user-1"
        });
        await dbContext.SaveChangesAsync();

        var service = new PublicShareService(dbContext);

        var share = await service.GetValidShareByTokenAsync(PublicShareEntityType.Dashboard, "expired");

        share.Should().BeNull();
    }

    [Fact]
    public async Task DisableAsync_Disables_Share()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.PublicShares.Add(new PublicShare
        {
            EntityType = PublicShareEntityType.Dashboard,
            EntityId = 2,
            Token = "token-2",
            IsEnabled = true,
            AccessLevel = ShareAccessLevel.View,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedByUserId = "user-1"
        });
        await dbContext.SaveChangesAsync();

        var service = new PublicShareService(dbContext);

        var share = await service.DisableAsync(PublicShareEntityType.Dashboard, 2, "user-2");

        share.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RegenerateAsync_Issues_New_Token()
    {
        using var dbContext = TestDbContextFactory.CreateDbContext();
        dbContext.PublicShares.Add(new PublicShare
        {
            EntityType = PublicShareEntityType.Dashboard,
            EntityId = 3,
            Token = "old-token",
            IsEnabled = true,
            AccessLevel = ShareAccessLevel.View,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedByUserId = "user-1"
        });
        await dbContext.SaveChangesAsync();

        var service = new PublicShareService(dbContext);

        var share = await service.RegenerateAsync(PublicShareEntityType.Dashboard, 3, "user-1", null);

        share.Token.Should().NotBe("old-token");
        share.IsEnabled.Should().BeTrue();
    }
}
