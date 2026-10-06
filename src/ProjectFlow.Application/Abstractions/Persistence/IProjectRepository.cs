using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    /// <summary>Includes deleted projects: their keys stay reserved.</summary>
    Task<bool> ExistsWithKeyAsync(Guid organizationId, ProjectKey key, CancellationToken cancellationToken);

    /// <summary>Loads a non-deleted project of the current organization with its members.</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Project project);
}
