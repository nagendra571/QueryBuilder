using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider.CreateProtector("QueryBuilder.DataSources.ConnectionString");

        await dbContext.Database.MigrateAsync();

        var roles = new[] { "Admin", "Editor", "Viewer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedData:AdminEmail"] ?? "admin@querybuilder.local";
        var adminPassword = configuration["SeedData:AdminPassword"] ?? "Admin123!";
        var adminUser = await userManager.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        if (!await dbContext.DataSources.AnyAsync())
        {
            var sampleConnection = configuration["SeedData:SampleDataSourceConnectionString"];
            if (!string.IsNullOrWhiteSpace(sampleConnection))
            {
                dbContext.DataSources.Add(new DataSource
                {
                    Name = "Sample SQL Server",
                    Description = "Local sample SQL Server connection.",
                    Type = DataSourceType.SqlServer,
                    ConnectionStringEncrypted = protector.Protect(sampleConnection),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }
        }

        if (!await dbContext.Queries.AnyAsync())
        {
            var dataSource = await dbContext.DataSources.OrderBy(ds => ds.Id).FirstOrDefaultAsync();
            if (dataSource != null && adminUser != null)
            {
                dbContext.Queries.Add(new Query
                {
                    Name = "Sample Objects",
                    Description = "Lists SQL Server objects for a quick sanity check.",
                    DataSourceId = dataSource.Id,
                    SqlText = "SELECT TOP (10) name, object_id, type_desc FROM sys.objects ORDER BY name;",
                    CreatedById = adminUser.Id,
                    UpdatedById = adminUser.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }
        }

        if (!await dbContext.Visualizations.AnyAsync())
        {
            var sampleQuery = await dbContext.Queries.OrderBy(q => q.Id).FirstOrDefaultAsync();
            if (sampleQuery != null)
            {
                dbContext.Visualizations.Add(new Visualization
                {
                    QueryId = sampleQuery.Id,
                    Name = "Sample Table",
                    Type = VisualizationType.Table,
                    ConfigJson = "{}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }
        }

        if (!await dbContext.Dashboards.AnyAsync())
        {
            var dashboard = new Dashboard
            {
                Name = "Sample Dashboard",
                Description = "MVP dashboard layout preview.",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Dashboards.Add(dashboard);
            await dbContext.SaveChangesAsync();

            var visualization = await dbContext.Visualizations.OrderBy(v => v.Id).FirstOrDefaultAsync();
            if (visualization != null)
            {
                dbContext.DashboardWidgets.Add(new DashboardWidget
                {
                    DashboardId = dashboard.Id,
                    VisualizationId = visualization.Id,
                    X = 0,
                    Y = 0,
                    Width = 6,
                    Height = 4
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
