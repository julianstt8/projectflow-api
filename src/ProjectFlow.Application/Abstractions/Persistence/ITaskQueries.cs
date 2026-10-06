using ProjectFlow.Application.Common;
using ProjectFlow.Application.Tasks;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ITaskQueries
{
    /// <summary>Task search with filters, sorting and paging (RF-11).</summary>
    Task<PagedResponse<TaskResponse>> SearchAsync(TaskSearchCriteria criteria, CancellationToken cancellationToken);

    Task<TaskResponse?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
}
