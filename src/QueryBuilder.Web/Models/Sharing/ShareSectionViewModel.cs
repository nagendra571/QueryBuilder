using Microsoft.AspNetCore.Mvc.Rendering;
using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.Sharing;

public class ShareSectionViewModel
{
    public int EntityId { get; set; }
    public ShareEntityType EntityType { get; set; }
    public IReadOnlyList<ShareListItemViewModel> Shares { get; set; } = Array.Empty<ShareListItemViewModel>();
    public List<SelectListItem> AvailableUsers { get; set; } = new();
    public List<SelectListItem> AvailableGroups { get; set; } = new();
}

public class ShareListItemViewModel
{
    public int Id { get; set; }
    public string GranteeName { get; set; } = string.Empty;
    public string GranteeType { get; set; } = string.Empty;
    public ShareAccessLevel AccessLevel { get; set; }
}

public class ShareCreateInputModel
{
    public int EntityId { get; set; }
    public ShareEntityType EntityType { get; set; }
    public string? UserId { get; set; }
    public int? GroupId { get; set; }
    public ShareAccessLevel AccessLevel { get; set; } = ShareAccessLevel.View;
}
