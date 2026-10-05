using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Application.Abstractions;

/// <summary>Resolves a user's role in an organization from the database, on every request (PRD 4.1).</summary>
public interface IOrganizationMembership
{
    /// <returns>The role, or <see langword="null"/> if the user is not a member (or the organization does not exist).</returns>
    Task<OrganizationRole?> GetRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}
