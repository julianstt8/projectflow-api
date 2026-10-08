using Microsoft.AspNetCore.Authorization;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.OpenApi;

/// <summary>
/// Who can call an endpoint and the errors its authorization can answer, read from the endpoint metadata
/// (<see cref="IAllowAnonymous"/>, the organization policies, <see cref="RequireProjectPermissionAttribute"/>) and the
/// domain permission matrix, so the reference cannot drift from the real rules.
/// </summary>
internal sealed record EndpointAccess(Localized WhoCanCall, IReadOnlyList<Error> Errors)
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("Authentication.Required", "Unauthorized");

    public static readonly Error NotAllowed =
        Error.Forbidden("Authorization.Forbidden", "Forbidden");

    public static readonly Error ValidationFailed =
        Error.Validation("Validation.Failed", "One or more validation errors occurred.");

    private static readonly ProjectRole[] ProjectRoles = Enum.GetValues<ProjectRole>();

    public static EndpointAccess For(IList<object> metadata)
    {
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return new(new("Anyone: no token needed.", "Cualquiera: no necesita token."), []);
        }

        if (metadata.OfType<RequireProjectPermissionAttribute>().FirstOrDefault() is { } projectPermission)
        {
            return ForProject(projectPermission.Permission);
        }

        var policies = metadata.OfType<IAuthorizeData>().Select(data => data.Policy).ToHashSet();
        if (policies.Contains(OrganizationPolicies.Admin))
        {
            return new(
                new("Admins of the organization.", "Administradores de la organización."),
                [NotAuthenticated, NotAllowed, OrganizationUseCaseErrors.NotFound]);
        }

        if (policies.Contains(OrganizationPolicies.Member))
        {
            return new(
                new("Any member of the organization.", "Cualquier miembro de la organización."),
                [NotAuthenticated, OrganizationUseCaseErrors.NotFound]);
        }

        return new(new("Any logged-in user.", "Cualquier usuario con sesión iniciada."), [NotAuthenticated]);
    }

    private static EndpointAccess ForProject(ProjectPermission permission)
    {
        var roles = ProjectRoles.Where(role => ProjectPermissions.Of(role).Contains(permission)).ToList();
        var who = new Localized(
            $"Admins of the organization and, in the project: {string.Join(", ", roles.Select(role => RoleNames[role].En))}.",
            $"Administradores de la organización y, en el proyecto: {string.Join(", ", roles.Select(role => RoleNames[role].Es))}.");

        if (permission == ProjectPermission.EditOwnTasks)
        {
            who = new(
                who.En + " Developers only on tasks they reported or are assigned to.",
                who.Es + " Los desarrolladores solo en tareas que crearon o tienen asignadas.");
        }

        // Every project role can view the project: a user who sees it is never refused this permission.
        Error[] errors = roles.Count == ProjectRoles.Length
            ? [NotAuthenticated, ProjectUseCaseErrors.NotFound]
            : [NotAuthenticated, NotAllowed, ProjectUseCaseErrors.NotFound];

        return new(who, errors);
    }

    private static readonly Dictionary<ProjectRole, Localized> RoleNames = new()
    {
        [ProjectRole.ProjectManager] = new("project manager", "jefe de proyecto"),
        [ProjectRole.Developer] = new("developer", "desarrollador"),
        [ProjectRole.Viewer] = new("viewer", "observador"),
    };
}
