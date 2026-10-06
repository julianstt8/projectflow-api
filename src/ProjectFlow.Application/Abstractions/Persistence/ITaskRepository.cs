using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ITaskRepository
{
    /// <summary>The task (not deleted), only if it belongs to <paramref name="projectId"/>.</summary>
    Task<TaskItem?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);

    /// <summary>Tasks of the sprint that are not done (and not deleted).</summary>
    Task<IReadOnlyList<TaskItem>> ListUnfinishedInSprintAsync(Guid sprintId, CancellationToken cancellationToken);

    void Add(TaskItem task);
}
