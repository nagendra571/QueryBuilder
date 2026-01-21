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
    public DbSet<Share> Shares => Set<Share>();
    public DbSet<PublicShare> PublicShares => Set<PublicShare>();

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
    }
}
