using System.ComponentModel.DataAnnotations;

namespace QueryBuilder.Domain.Entities;

public class FeatureFlag
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
