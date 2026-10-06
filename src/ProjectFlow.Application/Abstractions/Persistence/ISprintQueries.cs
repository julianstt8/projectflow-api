using ProjectFlow.Application.Sprints;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ISprintQueries
{
    Task<IReadOnlyList<SprintResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<SprintResponse?> GetAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken);
}
