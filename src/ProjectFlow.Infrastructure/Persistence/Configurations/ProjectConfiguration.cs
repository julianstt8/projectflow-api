using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();

        // Target of the composite foreign keys (project_id, organization_id) of every project child:
        // the database rejects a child that points to a project of another organization.
        builder.HasAlternateKey(project => new { project.Id, project.OrganizationId });

        builder.Property(project => project.OrganizationId);
        builder.Property(project => project.Key)
            .HasConversion(key => key.Value, value => ProjectKey.Create(value).Value)
            .HasMaxLength(ProjectKey.MaxLength);
        builder.HasIndex(project => new { project.OrganizationId, project.Key }).IsUnique();

        builder.Property(project => project.Name).HasMaxLength(Project.NameMaxLength);
        builder.Property(project => project.Description).HasMaxLength(Project.DescriptionMaxLength);
        builder.Property(project => project.NextTaskNumber);
        builder.Property(project => project.IsArchived);
        builder.Property(project => project.DeletedAt);
        builder.Property(project => project.CreatedAt);
        builder.Ignore(project => project.IsDeleted);
        builder.Property<uint>(PersistenceConstants.RowVersion).IsRowVersion();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(project => project.Members)
            .WithOne()
            .HasForeignKey(member => member.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members");
        builder.HasKey(member => new { member.ProjectId, member.UserId });

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(PersistenceConstants.EnumMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
