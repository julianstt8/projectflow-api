using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IOrganizationRepository
{
    Task<bool> ExistsWithSlugAsync(Slug slug, CancellationToken cancellationToken);

    /// <summary>Loads the organization with its members.</summary>
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Organization organization);
}
