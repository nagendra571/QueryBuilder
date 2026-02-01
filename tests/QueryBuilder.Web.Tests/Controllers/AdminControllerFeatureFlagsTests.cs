using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QueryBuilder.Web.Controllers;
using QueryBuilder.Web.Models.Admin;
using QueryBuilder.Web.Services;
using QueryBuilder.Web.Tests.TestHelpers;

namespace QueryBuilder.Web.Tests.Controllers;

public class AdminControllerFeatureFlagsTests
{
    [Fact]
    public void AdminController_HasAdminAuthorizeAttribute()
    {
        var attribute = typeof(AdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task UpdateFeature_WithEmptyKey_RedirectsWithMessage()
    {
        var service = new Mock<IFeatureFlagService>();
        var controller = new AdminController(TestDbContextFactory.CreateDbContext(), UserManagerMockHelper.Create("user-1").Object, service.Object)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(ControllerTestHelpers.CreateUser("user-1", "Admin"))
        };
        controller.TempData = ControllerTestHelpers.CreateTempData(controller);

        var result = await controller.UpdateFeature(string.Empty, true, null);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("FeatureFlags");
        controller.TempData["StatusMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateFeature_CallsServiceWithUpdatedBy()
    {
        var service = new Mock<IFeatureFlagService>();
        var controller = new AdminController(TestDbContextFactory.CreateDbContext(), UserManagerMockHelper.Create("user-1").Object, service.Object);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Name, "admin@local"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth");
        controller.ControllerContext = ControllerTestHelpers.CreateControllerContext(new ClaimsPrincipal(identity));
        controller.TempData = ControllerTestHelpers.CreateTempData(controller);

        await controller.UpdateFeature("NewAppearance", true, null);

        service.Verify(s => s.UpdateAsync("NewAppearance", true, "admin@local", null), Times.Once);
    }

    [Fact]
    public async Task CreateFeature_WithEmptyKey_RedirectsWithMessage()
    {
        var service = new Mock<IFeatureFlagService>();
        var controller = new AdminController(TestDbContextFactory.CreateDbContext(), UserManagerMockHelper.Create("user-1").Object, service.Object)
        {
            ControllerContext = ControllerTestHelpers.CreateControllerContext(ControllerTestHelpers.CreateUser("user-1", "Admin"))
        };
        controller.TempData = ControllerTestHelpers.CreateTempData(controller);

        var result = await controller.CreateFeature(new FeatureFlagCreateInputModel());

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("FeatureFlags");
        controller.TempData["StatusMessage"].Should().NotBeNull();
    }
}
