using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Web.Data;
using QueryBuilder.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddDataProtection();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<QueryRunner>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<SchemaBrowserService>();
builder.Services.AddScoped<PublicShareService>();
builder.Services.AddScoped<TableVisualizationConfigBuilder>();
builder.Services.AddScoped<QueryExecutionPreviewService>();
builder.Services.AddScoped<IQueryParameterService, QueryParameterService>();
builder.Services.AddScoped<IVisualizationService, VisualizationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isEmbed = path.StartsWith("/public/embed/", StringComparison.OrdinalIgnoreCase);

    context.Response.OnStarting(() =>
    {
        if (isEmbed)
        {
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors *";
            context.Response.Headers.Remove("X-Frame-Options");
        }
        else
        {
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

await SeedData.InitializeAsync(app.Services, app.Configuration);

app.Run();
