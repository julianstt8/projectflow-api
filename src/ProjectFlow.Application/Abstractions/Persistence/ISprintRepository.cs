using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ISprintRepository
{
    /// <summary>The sprint, only if it belongs to <paramref name="projectId"/>.</summary>
    Task<Sprint?> GetAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Sprint>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(Sprint sprint);
}
