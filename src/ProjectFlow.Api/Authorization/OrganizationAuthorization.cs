using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Api.Authorization;

/// <summary>Policies for endpoints under <c>/api/organizations/{organizationId}</c>.</summary>
public static class OrganizationPolicies
{
    /// <summary>The current user is a member (any role) of the organization in the route.</summary>
    public const string Member = "OrganizationMember";

    /// <summary>The current user is an admin of the organization in the route.</summary>
    public const string Admin = "OrganizationAdmin";

    public static AuthorizationBuilder AddOrganizationPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(Member, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OrganizationRoleRequirement(RequiredRole: null)))
            .AddPolicy(Admin, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Admin)));
}

public sealed record OrganizationRoleRequirement(OrganizationRole? RequiredRole) : IAuthorizationRequirement;

/// <summary>
/// Checks, against the database on every request, that the user belongs to the organization of the route
/// (and has the required role). This is what makes the route's <c>{organizationId}</c> trustworthy for the
/// organization query filters.
/// </summary>
internal sealed class OrganizationRoleHandler(
    IOrganizationMembership membership,
    ICurrentOrganization currentOrganization,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OrganizationRoleRequirement>
{
    private static readonly NotFoundFailureReason NotMember =
        new("Organization.NotFound", "The organization does not exist.");

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationRoleRequirement requirement)
    {
        var organizationId = currentOrganization.OrganizationId;
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (organizationId is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Fail(NotMember.For(this));
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var role = await membership.GetRoleAsync(organizationId.Value, userId, cancellationToken);

        if (role is null)
        {
            context.Fail(NotMember.For(this));
            return;
        }

        if (requirement.RequiredRole is { } requiredRole && role != requiredRole)
        {
            context.Fail(new AuthorizationFailureReason(this, $"The {requiredRole} role is required."));
            return;
        }

        context.Succeed(requirement);
    }
}
