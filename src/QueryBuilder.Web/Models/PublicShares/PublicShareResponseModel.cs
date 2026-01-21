using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.PublicShares;

public record PublicShareResponseModel(
    string Token,
    bool IsEnabled,
    DateTimeOffset? ExpiresAt)
{
    public PublicShareResponseModel(PublicShare share)
        : this(share.Token, share.IsEnabled, share.ExpiresAt)
    {
    }
}
