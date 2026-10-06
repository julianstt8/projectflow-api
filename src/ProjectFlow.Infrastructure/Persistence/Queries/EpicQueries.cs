using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Epics;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

internal sealed class EpicQueries(ApplicationDbContext dbContext) : IEpicQueries
{
    /// <summary>Open epics first, then by name.</summary>
    public async Task<IReadOnlyList<EpicResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await Project(dbContext.Epics
                .Where(epic => epic.ProjectId == projectId)
                .OrderBy(epic => epic.Status != EpicStatus.Open)
                .ThenBy(epic => epic.Name))
            .ToListAsync(cancellationToken);

    public Task<EpicResponse?> GetAsync(Guid projectId, Guid epicId, CancellationToken cancellationToken) =>
        Project(dbContext.Epics.Where(epic => epic.Id == epicId && epic.ProjectId == projectId))
            .SingleOrDefaultAsync(cancellationToken);

    // Task counts skip deleted tasks: the soft-delete filter applies inside the subqueries.
    private IQueryable<EpicResponse> Project(IQueryable<Epic> epics) =>
        epics
            .AsNoTracking()
            .Select(epic => new EpicResponse(
                epic.Id,
                epic.Name,
                epic.Description,
                epic.Status,
                dbContext.Tasks.Count(task => task.EpicId == epic.Id),
                dbContext.Tasks.Count(task => task.EpicId == epic.Id && task.Status == TaskItemStatus.Done)));
}
