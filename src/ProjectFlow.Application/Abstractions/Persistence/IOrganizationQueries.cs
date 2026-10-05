using ProjectFlow.Application.Organizations;

namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>Read side of organizations: projections straight to response models.</summary>
public interface IOrganizationQueries
{
    Task<IReadOnlyList<OrganizationSummaryResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<OrganizationDetailsResponse?> GetDetailsAsync(Guid organizationId, CancellationToken cancellationToken);
}
