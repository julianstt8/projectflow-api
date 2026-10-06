using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

/// <summary>Read-only projections; deleted tasks are hidden by the soft-delete filter.</summary>
internal sealed class TaskQueries(ApplicationDbContext dbContext) : ITaskQueries
{
    public async Task<IReadOnlyList<TaskResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await Rows(dbContext.Tasks.Where(task => task.ProjectId == projectId).OrderBy(task => task.Number))
            .ToListAsync(cancellationToken);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<TaskResponse?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var row = await Rows(dbContext.Tasks.Where(task => task.Id == taskId && task.ProjectId == projectId))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToResponse(row);
    }

    private IQueryable<TaskRow> Rows(IQueryable<TaskItem> tasks) =>
        tasks
            .AsNoTracking()
            .Join(
                dbContext.Projects,
                task => task.ProjectId,
                project => project.Id,
                (task, project) => new TaskRow
                {
                    Task = task,
                    ProjectKey = project.Key,
                    LabelIds = task.Labels.Select(label => label.LabelId).ToList(),
                });

    private static TaskResponse ToResponse(TaskRow row) =>
        new(
            row.Task.Id,
            $"{row.ProjectKey.Value}-{row.Task.Number}",
            row.Task.Number,
            row.Task.Title,
            row.Task.Description,
            row.Task.Type,
            row.Task.Priority,
            row.Task.Status,
            row.Task.StoryPoints,
            row.Task.ReporterId,
            row.Task.AssigneeId,
            row.Task.SprintId,
            row.Task.EpicId,
            row.LabelIds,
            row.Task.CreatedAt,
            row.Task.UpdatedAt);

    private sealed class TaskRow
    {
        public required TaskItem Task { get; init; }

        public required ProjectKey ProjectKey { get; init; }

        public required List<Guid> LabelIds { get; init; }
    }
}
