using Microsoft.AspNetCore.Mvc.Rendering;

namespace QueryBuilder.Web.Models.Admin;

public class GroupsViewModel
{
    public List<GroupItemViewModel> Groups { get; set; } = new();
    public List<SelectListItem> Users { get; set; } = new();
    public string? NewGroupName { get; set; }
}

public class GroupItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<GroupMemberViewModel> Members { get; set; } = new();
}

public class GroupMemberViewModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
