using ProjectFlow.Application.Epics;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IEpicQueries
{
    Task<IReadOnlyList<EpicResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<EpicResponse?> GetAsync(Guid projectId, Guid epicId, CancellationToken cancellationToken);
}
