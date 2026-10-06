using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>Projects of an organization and their members (RF-03, RF-04).</summary>
[Route("api/organizations/{organizationId:guid}/projects")]
public sealed class ProjectsController(ISender sender) : ApiControllerBase
{
    /// <summary>Creates a project; the current user becomes its project manager. Organization admins only.</summary>
    [HttpPost]
    [Authorize(Policy = OrganizationPolicies.Admin)]
    [ProducesResponseType<ProjectSummaryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid organizationId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateProjectCommand(organizationId, request.Key, request.Name, request.Description),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { organizationId, projectId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Projects the current user can see: all of them for organization admins, otherwise those with a project role.</summary>
    [HttpGet]
    [Authorize(Policy = OrganizationPolicies.Member)]
    [ProducesResponseType<IReadOnlyList<ProjectSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProjectsQuery(organizationId), cancellationToken));

    /// <summary>A project with its members.</summary>
    [HttpGet("{projectId:guid}")]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<ProjectDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProjectQuery(projectId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Renames a project or changes its description. Not allowed while archived (422).</summary>
    [HttpPut("{projectId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Update(Guid organizationId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        SendAsync(new UpdateProjectCommand(projectId, request.Name, request.Description), cancellationToken);

    /// <summary>Archives a project: it stays visible but read-only.</summary>
    [HttpPost("{projectId:guid}/archive")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Archive(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        SendAsync(new ArchiveProjectCommand(projectId), cancellationToken);

    [HttpPost("{projectId:guid}/unarchive")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Unarchive(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        SendAsync(new UnarchiveProjectCommand(projectId), cancellationToken);

    /// <summary>Soft-deletes a project (hidden from every query, kept in the database). Organization admins only.</summary>
    [HttpDelete("{projectId:guid}")]
    [Authorize(Policy = OrganizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        SendAsync(new DeleteProjectCommand(projectId), cancellationToken);

    /// <summary>Gives a project role to a member of the organization.</summary>
    [HttpPost("{projectId:guid}/members")]
    [RequireProjectPermission(ProjectPermission.ManageProjectMembers)]
    [ProducesResponseType<ProjectMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddMember(Guid organizationId, Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddProjectMemberCommand(organizationId, projectId, request.Email, request.Role), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Changes a member's project role. The last project manager cannot be demoted (422).</summary>
    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    [RequireProjectPermission(ProjectPermission.ManageProjectMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ChangeMemberRole(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        ChangeProjectMemberRoleRequest request,
        CancellationToken cancellationToken) =>
        SendAsync(new ChangeProjectMemberRoleCommand(projectId, userId, request.Role), cancellationToken);

    /// <summary>Removes a member from the project. The last project manager cannot be removed (422).</summary>
    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    [RequireProjectPermission(ProjectPermission.ManageProjectMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoveMember(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveProjectMemberCommand(projectId, userId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Domain.Common.Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

public sealed record CreateProjectRequest(string Key, string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description);

public sealed record AddProjectMemberRequest(string Email, ProjectRole Role);

public sealed record ChangeProjectMemberRoleRequest(ProjectRole Role);
