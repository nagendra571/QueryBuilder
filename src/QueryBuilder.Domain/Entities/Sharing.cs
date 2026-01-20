namespace QueryBuilder.Domain.Entities;

public enum ShareEntityType
{
    Query = 1,
    Dashboard = 2
}

public enum ShareAccessLevel
{
    View = 1,
    Edit = 2
}

public class Share
{
    public int Id { get; set; }
    public ShareEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public ShareAccessLevel AccessLevel { get; set; }
    public string? UserId { get; set; }
    public int? GroupId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Group? Group { get; set; }
}

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<Share> Shares { get; set; } = new List<Share>();
}

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Group? Group { get; set; }
}
