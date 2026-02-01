using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QueryBuilder.Domain.Entities;

namespace QueryBuilder.Web.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<Query> Queries => Set<Query>();
    public DbSet<QueryExecution> QueryExecutions => Set<QueryExecution>();
    public DbSet<Visualization> Visualizations => Set<Visualization>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Share> Shares => Set<Share>();
    public DbSet<PublicShare> PublicShares => Set<PublicShare>();
    public DbSet<QueryParameterDefinition> QueryParameterDefinitions => Set<QueryParameterDefinition>();
    public DbSet<DashboardParameterControl> DashboardParameterControls => Set<DashboardParameterControl>();
    public DbSet<DashboardParameterMapping> DashboardParameterMappings => Set<DashboardParameterMapping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Group>(entity =>
        {
            entity.HasIndex(g => g.Name).IsUnique();
        });

        builder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
        });

        builder.Entity<FeatureFlag>(entity =>
        {
            entity.Property(f => f.Key).HasMaxLength(100);
            entity.Property(f => f.Description).HasMaxLength(500);
            entity.Property(f => f.UpdatedBy).HasMaxLength(256);
            entity.Property(f => f.RowVersion).IsRowVersion();
            entity.HasIndex(f => f.Key).IsUnique();
        });

        builder.Entity<Share>(entity =>
        {
            entity.HasIndex(s => new { s.EntityType, s.EntityId });
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.GroupId);
        });

        builder.Entity<PublicShare>(entity =>
        {
            entity.Property(s => s.Token).HasMaxLength(200);
            entity.HasIndex(s => s.Token).IsUnique();
            entity.HasIndex(s => new { s.EntityType, s.EntityId });
        });

        builder.Entity<QueryParameterDefinition>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Title).HasMaxLength(200);
            entity.Property(p => p.SettingsJson).HasDefaultValue("{}");
            entity.HasIndex(p => new { p.QueryId, p.Name }).IsUnique();
            entity.HasIndex(p => p.QueryId);
        });

        builder.Entity<Visualization>(entity =>
        {
            entity.Property(v => v.IsDeleted).HasDefaultValue(false);
            entity.HasQueryFilter(v => !v.IsDeleted);
            entity.HasIndex(v => v.IsDeleted);
        });

        builder.Entity<DashboardParameterControl>(entity =>
        {
            entity.Property(p => p.Key).HasMaxLength(200);
            entity.Property(p => p.Title).HasMaxLength(200);
            entity.Property(p => p.SettingsJson).HasDefaultValue("{}");
            entity.HasIndex(p => new { p.DashboardId, p.Key }).IsUnique();
            entity.HasIndex(p => p.DashboardId);
        });

        builder.Entity<DashboardParameterMapping>(entity =>
        {
            entity.Property(p => p.QueryParamKey).HasMaxLength(200);
            entity.Property(p => p.ControlKey).HasMaxLength(200);
            entity.HasIndex(p => new { p.DashboardWidgetId, p.QueryParamKey }).IsUnique();
            entity.HasIndex(p => p.QueryId);
            entity.HasOne(p => p.Query)
                .WithMany()
                .HasForeignKey(p => p.QueryId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
