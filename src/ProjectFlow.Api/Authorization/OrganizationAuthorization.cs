using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
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
    public const string NotMemberReason = "NotOrganizationMember";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationRoleRequirement requirement)
    {
        var organizationId = currentOrganization.OrganizationId;
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (organizationId is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Fail(new AuthorizationFailureReason(this, NotMemberReason));
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var role = await membership.GetRoleAsync(organizationId.Value, userId, cancellationToken);

        if (role is null)
        {
            context.Fail(new AuthorizationFailureReason(this, NotMemberReason));
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

/// <summary>
/// Answers 404 instead of 403 when the user is not a member, so outsiders cannot even tell whether an
/// organization exists. Members without the required role still get 403.
/// </summary>
internal sealed class OrganizationAuthorizationResultHandler(IProblemDetailsService problemDetailsService)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var notMember = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(reason => reason.Message == OrganizationRoleHandler.NotMemberReason) == true;

        if (!notMember)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "The organization does not exist.",
                Extensions = { ["code"] = "Organization.NotFound" },
            },
        });
    }
}
