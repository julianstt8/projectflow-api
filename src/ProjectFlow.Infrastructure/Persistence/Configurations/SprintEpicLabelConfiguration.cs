using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Infrastructure.Persistence.Configurations;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("sprints");
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id).ValueGeneratedNever();

        builder.Property(sprint => sprint.ProjectId);
        builder.Property(sprint => sprint.OrganizationId);
        builder.Property(sprint => sprint.Name).HasMaxLength(Sprint.NameMaxLength);
        builder.Property(sprint => sprint.Goal).HasMaxLength(Sprint.GoalMaxLength);
        builder.Property(sprint => sprint.StartDate);
        builder.Property(sprint => sprint.EndDate);
        builder.Property(sprint => sprint.Status)
            .HasConversion<string>()
            .HasMaxLength(PersistenceConstants.EnumMaxLength);
        builder.Property<uint>(PersistenceConstants.RowVersion).IsRowVersion();

        builder.HasProjectForeignKey(sprint => new { sprint.ProjectId, sprint.OrganizationId });
        builder.HasIndex(sprint => new { sprint.OrganizationId, sprint.ProjectId });

        // RF-05: at most one active sprint per project, enforced by the database too.
        builder.HasIndex(sprint => sprint.ProjectId)
            .IsUnique()
            .HasFilter($"status = '{nameof(SprintStatus.Active)}'")
            .HasDatabaseName("ix_sprints_project_id_active");
    }
}

internal sealed class EpicConfiguration : IEntityTypeConfiguration<Epic>
{
    public void Configure(EntityTypeBuilder<Epic> builder)
    {
        builder.ToTable("epics");
        builder.HasKey(epic => epic.Id);
        builder.Property(epic => epic.Id).ValueGeneratedNever();

        builder.Property(epic => epic.ProjectId);
        builder.Property(epic => epic.OrganizationId);
        builder.Property(epic => epic.Name).HasMaxLength(Epic.NameMaxLength);
        builder.Property(epic => epic.Description).HasMaxLength(Epic.DescriptionMaxLength);
        builder.Property(epic => epic.Status)
            .HasConversion<string>()
            .HasMaxLength(PersistenceConstants.EnumMaxLength);

        builder.HasProjectForeignKey(epic => new { epic.ProjectId, epic.OrganizationId });
        builder.HasIndex(epic => new { epic.OrganizationId, epic.ProjectId });
    }
}

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Id).ValueGeneratedNever();

        builder.Property(label => label.ProjectId);
        builder.Property(label => label.OrganizationId);
        builder.Property(label => label.Name).HasMaxLength(Label.NameMaxLength);
        builder.Property(label => label.Color).HasMaxLength(7);

        builder.HasProjectForeignKey(label => new { label.ProjectId, label.OrganizationId });
        builder.HasIndex(label => new { label.OrganizationId, label.ProjectId });
    }
}

internal static class ProjectChildConfigurationExtensions
{
    /// <summary>Composite foreign key (project_id, organization_id) → projects (id, organization_id).</summary>
    public static void HasProjectForeignKey<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<Func<TEntity, object?>> foreignKey)
        where TEntity : class
    {
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(foreignKey)
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
