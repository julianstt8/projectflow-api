using ProjectFlow.Domain.Epics;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IEpicRepository
{
    /// <summary>The epic, only if it belongs to <paramref name="projectId"/>.</summary>
    Task<Epic?> GetAsync(Guid projectId, Guid epicId, CancellationToken cancellationToken);

    void Add(Epic epic);
}
