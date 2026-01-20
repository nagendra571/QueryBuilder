using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Models.DataSources;

public class DataSourceEditViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DataSourceType Type { get; set; } = DataSourceType.SqlServer;

    [Display(Name = "Connection String")]
    public string? ConnectionString { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public string? SubmitAction { get; set; }
}
