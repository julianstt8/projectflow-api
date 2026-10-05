namespace ProjectFlow.Application.Abstractions;

/// <summary>
/// The organization the current operation works in. Every query on organization-owned data is
/// filtered by it; when it is <see langword="null"/>, no organization-owned data is returned.
/// </summary>
/// <remarks>
/// The value comes from the request route. It is trusted only because every organization-scoped endpoint
/// is protected by an organization policy that checks the current user is a member (see the Api project).
/// </remarks>
public interface ICurrentOrganization
{
    Guid? OrganizationId { get; }
}
