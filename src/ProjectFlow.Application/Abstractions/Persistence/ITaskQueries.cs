using ProjectFlow.Application.Tasks;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ITaskQueries
{
    /// <summary>Tasks of the project by number. Search, filters and pagination come with RF-11.</summary>
    Task<IReadOnlyList<TaskResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<TaskResponse?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
}
