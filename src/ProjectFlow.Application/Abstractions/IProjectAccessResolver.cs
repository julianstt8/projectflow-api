using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Abstractions;

/// <summary>Resolves what a user can do in a project, from the database, on every request (PRD 4.1).</summary>
public interface IProjectAccessResolver
{
    /// <returns>
    /// The user's access, or <see langword="null"/> when the project does not exist in that organization
    /// (or is deleted) or the user is not a member of the organization.
    /// </returns>
    Task<ProjectAccess?> GetAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken);
}
