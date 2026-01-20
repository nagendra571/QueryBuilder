using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Moq;
using QueryBuilder.Web.Data;

namespace QueryBuilder.Web.Tests.TestHelpers;

public static class UserManagerMockHelper
{
    public static Mock<UserManager<IdentityUser>> Create(string userId, ApplicationDbContext? dbContext = null)
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        var manager = new Mock<UserManager<IdentityUser>>(
            store.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        manager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);
        if (dbContext != null)
        {
            manager.SetupGet(m => m.Users).Returns(dbContext.Users);
        }

        return manager;
    }
}
