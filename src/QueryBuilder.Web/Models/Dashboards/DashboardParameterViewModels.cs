using System.ComponentModel.DataAnnotations;
using QueryBuilder.Domain.Entities;
using QueryBuilder.Web.Models.Queries;

namespace QueryBuilder.Web.Models.Dashboards;

public class DashboardParameterControlViewModel
{
    public int? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public QueryParameterType Type { get; set; } = QueryParameterType.Text;
    public string? DefaultValue { get; set; }
    public DashboardParameterPlacement Placement { get; set; } = DashboardParameterPlacement.TopBar;
    public string? SettingsJson { get; set; }
}

public class DashboardWidgetParameterViewModel
{
    public int WidgetId { get; set; }
    public int VisualizationId { get; set; }
    public int QueryId { get; set; }
    public string VisualizationName { get; set; } = string.Empty;
    public IReadOnlyList<QueryParameterDefinitionViewModel> Parameters { get; set; } = Array.Empty<QueryParameterDefinitionViewModel>();
    public IReadOnlyList<DashboardParameterMappingViewModel> Mappings { get; set; } = Array.Empty<DashboardParameterMappingViewModel>();
}

public class DashboardParameterMappingViewModel
{
    public string QueryParamKey { get; set; } = string.Empty;
    public DashboardParameterBindingType BindingType { get; set; } = DashboardParameterBindingType.InlineControl;
    public string? ControlKey { get; set; }
    public string? StaticValue { get; set; }
}

public class DashboardParameterStateViewModel
{
    public IReadOnlyDictionary<string, string> DashboardControlValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> InlineValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public class DashboardParameterControlInputModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public QueryParameterType Type { get; set; } = QueryParameterType.Text;

    [StringLength(400)]
    public string? DefaultValue { get; set; }

    public DashboardParameterPlacement Placement { get; set; } = DashboardParameterPlacement.TopBar;

    public string? SettingsJson { get; set; }
}

public class DashboardParameterControlsUpdateModel
{
    public int DashboardId { get; set; }
    public List<DashboardParameterControlInputModel> Controls { get; set; } = new();
}

public class DashboardParameterMappingInputModel
{
    public int DashboardWidgetId { get; set; }
    public int QueryId { get; set; }

    [Required]
    public string QueryParamKey { get; set; } = string.Empty;

    public DashboardParameterBindingType BindingType { get; set; } = DashboardParameterBindingType.InlineControl;
    public string? ControlKey { get; set; }
    public string? StaticValue { get; set; }
    public string? StaticValueStart { get; set; }
    public string? StaticValueEnd { get; set; }
}

public class DashboardParameterMappingsUpdateModel
{
    public int DashboardId { get; set; }
    public List<DashboardParameterMappingInputModel> Mappings { get; set; } = new();
}

public class DashboardWidgetMappingInputModel
{
    public int QueryId { get; set; }

    [Required]
    public string QueryParamKey { get; set; } = string.Empty;

    public DashboardParameterBindingType BindingType { get; set; } = DashboardParameterBindingType.InlineControl;
    public string? ControlKey { get; set; }
    public string? ControlTitle { get; set; }
    public bool CreateControl { get; set; }
    public string? StaticValue { get; set; }
    public string? StaticValueStart { get; set; }
    public string? StaticValueEnd { get; set; }
}

public class AddDashboardWidgetRequestModel
{
    public int DashboardId { get; set; }
    public int VisualizationId { get; set; }
    public List<DashboardWidgetMappingInputModel> Mappings { get; set; } = new();
}
