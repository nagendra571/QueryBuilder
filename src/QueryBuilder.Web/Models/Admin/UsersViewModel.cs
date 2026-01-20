namespace QueryBuilder.Web.Models.Admin;

public class UsersViewModel
{
    public List<UserItemViewModel> Users { get; set; } = new();
    public List<string> Roles { get; set; } = new();
}

public class UserItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
