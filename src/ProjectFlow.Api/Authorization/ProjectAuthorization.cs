using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Authorization;

/// <summary>
/// Protects an endpoint under <c>/api/organizations/{organizationId}/projects/{projectId}/...</c> with a
/// permission of the PRD matrix (RF-03). Both route values must have exactly these names.
/// Users who cannot see the project get 404; users who see it but lack the permission get 403.
/// </summary>
/// <example><c>[RequireProjectPermission(ProjectPermission.ManageSprints)]</c></example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireProjectPermissionAttribute(ProjectPermission permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public const string ProjectRouteValue = "projectId";

    public ProjectPermission Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new ProjectPermissionRequirement(Permission)];
}

public sealed record ProjectPermissionRequirement(ProjectPermission Permission) : IAuthorizationRequirement;

/// <summary>
/// Resolves the user's project access from the database on every request (organization admins have every
/// permission) and checks the required permission. Fine-grained rules on a specific resource, like
/// "developers edit only their own tasks", are checked by the use case with <see cref="ProjectAccess"/>.
/// </summary>
internal sealed class ProjectPermissionHandler(
    IProjectAccessResolver projectAccess,
    ICurrentOrganization currentOrganization,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ProjectPermissionRequirement>
{
    private static readonly NotFoundFailureReason ProjectNotFound =
        new("Project.NotFound", "The project does not exist.");

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectPermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var organizationId = currentOrganization.OrganizationId;
        var projectIdValue = httpContext?.GetRouteValue(RequireProjectPermissionAttribute.ProjectRouteValue) as string;

        if (organizationId is null
            || !Guid.TryParse(projectIdValue, out var projectId)
            || !Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            context.Fail(ProjectNotFound.For(this));
            return;
        }

        var access = await projectAccess.GetAsync(
            organizationId.Value,
            projectId,
            userId,
            httpContext?.RequestAborted ?? CancellationToken.None);

        if (access is not { HasAccess: true })
        {
            context.Fail(ProjectNotFound.For(this));
            return;
        }

        if (!access.Has(requirement.Permission))
        {
            context.Fail(new AuthorizationFailureReason(this, $"The {requirement.Permission} permission is required."));
            return;
        }

        context.Succeed(requirement);
    }
}
