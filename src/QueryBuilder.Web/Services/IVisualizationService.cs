using System.Security.Claims;
using QueryBuilder.Web.Models.Visualizations;

namespace QueryBuilder.Web.Services;

public interface IVisualizationService
{
    Task<VisualizationDeleteResult> DeleteVisualizationAsync(int visualizationId, ClaimsPrincipal user);
}
