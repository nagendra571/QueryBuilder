namespace QueryBuilder.Domain.Entities;

public enum PublicShareEntityType
{
    Dashboard = 1,
    Widget = 2
}

public class PublicShare
{
    public int Id { get; set; }
    public PublicShareEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public ShareAccessLevel AccessLevel { get; set; } = ShareAccessLevel.View;
    public string Token { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? LastAccessedAt { get; set; }
}
