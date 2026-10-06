using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    /// <summary>Includes deleted projects: their keys stay reserved.</summary>
    Task<bool> ExistsWithKeyAsync(Guid organizationId, ProjectKey key, CancellationToken cancellationToken);

    /// <summary>Loads a non-deleted project of the current organization with its members.</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the project row until the current transaction ends, so concurrent requests that number tasks
    /// wait for each other instead of reading the same <see cref="Project.NextTaskNumber"/> (RF-04).
    /// </summary>
    Task LockForTaskNumberingAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(Project project);
}
