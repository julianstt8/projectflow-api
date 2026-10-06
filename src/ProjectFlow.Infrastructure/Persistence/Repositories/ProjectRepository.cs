using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRepository(ApplicationDbContext dbContext) : IProjectRepository
{
    public Task<bool> ExistsWithKeyAsync(Guid organizationId, ProjectKey key, CancellationToken cancellationToken) =>
        dbContext.Projects
            .IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter])
            .AnyAsync(project => project.OrganizationId == organizationId && project.Key == key, cancellationToken);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Projects
            .Include(project => project.Members)
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task LockForTaskNumberingAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync($"SELECT 1 FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

    public void Add(Project project) => dbContext.Projects.Add(project);
}
