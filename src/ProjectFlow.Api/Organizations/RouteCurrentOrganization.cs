using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Api.Organizations;

/// <summary>
/// Reads the current organization from the <c>{organizationId}</c> route value of
/// organization-scoped endpoints (e.g. <c>/api/organizations/{organizationId}/projects</c>).
/// </summary>
internal sealed class RouteCurrentOrganization(IHttpContextAccessor httpContextAccessor) : ICurrentOrganization
{
    public const string RouteValueName = "organizationId";

    public Guid? OrganizationId =>
        httpContextAccessor.HttpContext?.GetRouteValue(RouteValueName) is string value
        && Guid.TryParse(value, out var organizationId)
            ? organizationId
            : null;
}
