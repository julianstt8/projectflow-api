using ProjectFlow.Application.Activity;
using ProjectFlow.Application.Common;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IActivityQueries
{
    /// <summary>Newest first; optionally only the history of one entity (e.g. a task).</summary>
    Task<PagedResponse<ActivityResponse>> ListAsync(Guid projectId, Guid? entityId, int page, int pageSize, CancellationToken cancellationToken);
}
