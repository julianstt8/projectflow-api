using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Epics;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class EpicRepository(ApplicationDbContext dbContext) : IEpicRepository
{
    public Task<Epic?> GetAsync(Guid projectId, Guid epicId, CancellationToken cancellationToken) =>
        dbContext.Epics.SingleOrDefaultAsync(epic => epic.Id == epicId && epic.ProjectId == projectId, cancellationToken);

    public void Add(Epic epic) => dbContext.Epics.Add(epic);
}
