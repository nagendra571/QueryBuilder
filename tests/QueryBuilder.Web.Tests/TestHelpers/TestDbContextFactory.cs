using Microsoft.EntityFrameworkCore;
using QueryBuilder.Web.Data;

namespace QueryBuilder.Web.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
