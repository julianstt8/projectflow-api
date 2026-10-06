using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class SprintRepository(ApplicationDbContext dbContext) : ISprintRepository
{
    public Task<Sprint?> GetAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken) =>
        dbContext.Sprints.SingleOrDefaultAsync(sprint => sprint.Id == sprintId && sprint.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyList<Sprint>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Sprints.Where(sprint => sprint.ProjectId == projectId).ToListAsync(cancellationToken);

    public void Add(Sprint sprint) => dbContext.Sprints.Add(sprint);
}
