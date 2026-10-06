using ProjectFlow.Application.Projects;

namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>Read side of projects: projections straight to response models.</summary>
public interface IProjectQueries
{
    /// <summary>Organization admins see every project; other members only projects where they have a role.</summary>
    Task<IReadOnlyList<ProjectSummaryResponse>> ListVisibleAsync(
        Guid organizationId,
        Guid userId,
        bool isOrganizationAdmin,
        CancellationToken cancellationToken);

    Task<ProjectDetailsResponse?> GetDetailsAsync(Guid projectId, CancellationToken cancellationToken);
}
