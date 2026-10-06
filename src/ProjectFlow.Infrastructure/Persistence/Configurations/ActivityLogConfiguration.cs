using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectFlow.Domain.Activity;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Configurations;

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();

        builder.Property(log => log.OrganizationId);
        builder.Property(log => log.ProjectId);
        builder.Property(log => log.ActorId);
        builder.Property(log => log.EntityType).HasMaxLength(ActivityLog.EntityTypeMaxLength);
        builder.Property(log => log.EntityId);
        builder.Property(log => log.Action).HasMaxLength(ActivityLog.ActionMaxLength);
        builder.Property(log => log.OldValue).HasMaxLength(ActivityLog.ValueMaxLength);
        builder.Property(log => log.NewValue).HasMaxLength(ActivityLog.ValueMaxLength);
        builder.Property(log => log.CreatedAt);

        builder.HasProjectForeignKey(log => new { log.ProjectId, log.OrganizationId });
        builder.HasOne<User>().WithMany().HasForeignKey(log => log.ActorId).OnDelete(DeleteBehavior.Restrict);

        // Reading the log: newest first per project, or the history of one entity.
        builder.HasIndex(log => new { log.OrganizationId, log.ProjectId, log.CreatedAt });
        builder.HasIndex(log => new { log.OrganizationId, log.EntityId });
    }
}
