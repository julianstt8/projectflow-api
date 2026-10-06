using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Activity;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentOrganization currentOrganization)
    : DbContext(options)
{
    /// <summary>Global query filter that hides data of other organizations.</summary>
    public const string OrganizationFilter = "Organization";

    /// <summary>Global query filter that hides soft-deleted rows. Can be disabled alone with <c>IgnoreQueryFilters([SoftDeleteFilter])</c>.</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<Epic> Epics => Set<Epic>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    // Read by the query filters on every query; EF Core parameterizes it per DbContext instance.
    private Guid? CurrentOrganizationId => currentOrganization.OrganizationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Join tables (project_members, task_labels) are reached only through their filtered parent.
        ApplyOrganizationFilter<Project>(modelBuilder);
        ApplyOrganizationFilter<Sprint>(modelBuilder);
        ApplyOrganizationFilter<Epic>(modelBuilder);
        ApplyOrganizationFilter<TaskItem>(modelBuilder);
        ApplyOrganizationFilter<Label>(modelBuilder);
        ApplyOrganizationFilter<Comment>(modelBuilder);
        ApplyOrganizationFilter<ActivityLog>(modelBuilder);

        ApplySoftDeleteFilter<Project>(modelBuilder);
        ApplySoftDeleteFilter<TaskItem>(modelBuilder);
    }

    private void ApplyOrganizationFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IOrganizationOwned =>
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(OrganizationFilter, entity => entity.OrganizationId == CurrentOrganizationId);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(SoftDeleteFilter, entity => entity.DeletedAt == null);
}
