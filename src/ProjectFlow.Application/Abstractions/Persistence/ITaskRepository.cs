using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ITaskRepository
{
    /// <summary>Tasks of the sprint that are not done (and not deleted).</summary>
    Task<IReadOnlyList<TaskItem>> ListUnfinishedInSprintAsync(Guid sprintId, CancellationToken cancellationToken);
}
