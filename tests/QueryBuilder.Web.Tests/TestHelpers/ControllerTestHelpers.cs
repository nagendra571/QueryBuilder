using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace QueryBuilder.Web.Tests.TestHelpers;

public static class ControllerTestHelpers
{
    public static ClaimsPrincipal CreateUser(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    public static ControllerContext CreateControllerContext(ClaimsPrincipal user)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };
    }

    public static ITempDataDictionary CreateTempData(Controller controller)
    {
        var provider = new Mock<ITempDataProvider>();
        return new TempDataDictionary(controller.HttpContext, provider.Object);
    }
}
