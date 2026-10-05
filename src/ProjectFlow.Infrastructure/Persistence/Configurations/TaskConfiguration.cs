using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Configurations;

internal sealed class TaskConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).ValueGeneratedNever();

        // Target of the composite foreign key (task_id, organization_id) of comments.
        builder.HasAlternateKey(task => new { task.Id, task.OrganizationId });

        builder.Property(task => task.ProjectId);
        builder.Property(task => task.OrganizationId);
        builder.Property(task => task.Number);
        builder.Property(task => task.SprintId);
        builder.Property(task => task.EpicId);
        builder.Property(task => task.AssigneeId);
        builder.Property(task => task.ReporterId);
        builder.Property(task => task.Title).HasMaxLength(TaskItem.TitleMaxLength);
        builder.Property(task => task.Description).HasMaxLength(TaskItem.DescriptionMaxLength);
        builder.Property(task => task.Type).HasConversion<string>().HasMaxLength(PersistenceConstants.EnumMaxLength);
        builder.Property(task => task.Priority).HasConversion<string>().HasMaxLength(PersistenceConstants.EnumMaxLength);
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(PersistenceConstants.EnumMaxLength);
        builder.Property(task => task.StoryPoints);
        builder.Property(task => task.DeletedAt);
        builder.Property(task => task.CreatedAt);
        builder.Property(task => task.UpdatedAt);
        builder.Ignore(task => task.IsDeleted);
        builder.Property<uint>(PersistenceConstants.RowVersion).IsRowVersion();

        builder.HasProjectForeignKey(task => new { task.ProjectId, task.OrganizationId });
        builder.HasIndex(task => new { task.ProjectId, task.Number }).IsUnique();

        builder.HasOne<Sprint>().WithMany().HasForeignKey(task => task.SprintId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Epic>().WithMany().HasForeignKey(task => task.EpicId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(task => task.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(task => task.ReporterId).OnDelete(DeleteBehavior.Restrict);

        // Filters of RF-11, starting with organization_id as the data model requires.
        builder.HasIndex(task => new { task.OrganizationId, task.ProjectId, task.Status });
        builder.HasIndex(task => new { task.OrganizationId, task.AssigneeId });
        builder.HasIndex(task => new { task.OrganizationId, task.SprintId });

        builder.HasMany(task => task.Labels)
            .WithOne()
            .HasForeignKey(taskLabel => taskLabel.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(task => task.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TaskLabelConfiguration : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.ToTable("task_labels");
        builder.HasKey(taskLabel => new { taskLabel.TaskId, taskLabel.LabelId });

        builder.HasOne<Label>()
            .WithMany()
            .HasForeignKey(taskLabel => taskLabel.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();

        builder.Property(comment => comment.TaskId);
        builder.Property(comment => comment.OrganizationId);
        builder.Property(comment => comment.AuthorId);
        builder.Property(comment => comment.Body).HasMaxLength(Comment.BodyMaxLength);
        builder.Property(comment => comment.CreatedAt);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(comment => new { comment.TaskId, comment.OrganizationId })
            .HasPrincipalKey(task => new { task.Id, task.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comment => new { comment.OrganizationId, comment.TaskId });
    }
}
