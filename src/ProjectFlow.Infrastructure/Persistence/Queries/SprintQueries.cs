using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

internal sealed class SprintQueries(ApplicationDbContext dbContext) : ISprintQueries
{
    /// <summary>Dated sprints in date order, then undated ones.</summary>
    public async Task<IReadOnlyList<SprintResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await Project(dbContext.Sprints
                .Where(sprint => sprint.ProjectId == projectId)
                .OrderBy(sprint => sprint.StartDate == null)
                .ThenBy(sprint => sprint.StartDate)
                .ThenBy(sprint => sprint.Name))
            .ToListAsync(cancellationToken);

    public Task<SprintResponse?> GetAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken) =>
        Project(dbContext.Sprints.Where(sprint => sprint.Id == sprintId && sprint.ProjectId == projectId))
            .SingleOrDefaultAsync(cancellationToken);

    // Task counts skip deleted tasks: the soft-delete filter applies inside the subquery too.
    private IQueryable<SprintResponse> Project(IQueryable<Sprint> sprints) =>
        sprints
            .AsNoTracking()
            .Select(sprint => new SprintResponse(
                sprint.Id,
                sprint.Name,
                sprint.Goal,
                sprint.StartDate,
                sprint.EndDate,
                sprint.Status,
                dbContext.Tasks.Count(task => task.SprintId == sprint.Id)));
}
