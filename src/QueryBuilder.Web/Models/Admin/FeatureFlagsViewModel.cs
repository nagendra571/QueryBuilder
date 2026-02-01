namespace QueryBuilder.Web.Models.Admin;

public class FeatureFlagsViewModel
{
    public IReadOnlyList<FeatureFlagItemViewModel> Flags { get; set; } = Array.Empty<FeatureFlagItemViewModel>();
}

public class FeatureFlagItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? RowVersion { get; set; }
}

public class FeatureFlagCreateInputModel
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
}
