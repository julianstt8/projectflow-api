namespace ProjectFlow.Application.Abstractions;

/// <summary>
/// The organization the current operation works in. Every query on organization-owned data is
/// filtered by it; when it is <see langword="null"/>, no organization-owned data is returned.
/// </summary>
/// <remarks>
/// The value comes from the request and is not trusted by itself: authorization (#10, #11) must verify
/// that the current user is a member of this organization before any handler runs.
/// </remarks>
public interface ICurrentOrganization
{
    Guid? OrganizationId { get; }
}
