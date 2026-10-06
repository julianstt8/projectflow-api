using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.IntegrationTests.Authorization;

/// <summary>
/// Test-only endpoints, one per permission, registered just for <see cref="ProjectAuthorizationTests"/>.
/// They let the real authorization pipeline be tested before the project endpoints exist (#13 onwards).
/// </summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/authorization-probe")]
public sealed class ProjectAuthorizationProbeController : ControllerBase
{
    [HttpGet(nameof(ProjectPermission.ViewProject))]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    public IActionResult ViewProject() => Ok();

    [HttpGet(nameof(ProjectPermission.EditProject))]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    public IActionResult EditProject() => Ok();

    [HttpGet(nameof(ProjectPermission.ManageProjectMembers))]
    [RequireProjectPermission(ProjectPermission.ManageProjectMembers)]
    public IActionResult ManageProjectMembers() => Ok();

    [HttpGet(nameof(ProjectPermission.ManageSprints))]
    [RequireProjectPermission(ProjectPermission.ManageSprints)]
    public IActionResult ManageSprints() => Ok();

    [HttpGet(nameof(ProjectPermission.ManageEpics))]
    [RequireProjectPermission(ProjectPermission.ManageEpics)]
    public IActionResult ManageEpics() => Ok();

    [HttpGet(nameof(ProjectPermission.CreateTasks))]
    [RequireProjectPermission(ProjectPermission.CreateTasks)]
    public IActionResult CreateTasks() => Ok();

    [HttpGet(nameof(ProjectPermission.EditAnyTask))]
    [RequireProjectPermission(ProjectPermission.EditAnyTask)]
    public IActionResult EditAnyTask() => Ok();

    [HttpGet(nameof(ProjectPermission.EditOwnTasks))]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    public IActionResult EditOwnTasks() => Ok();

    [HttpGet(nameof(ProjectPermission.ReopenTasks))]
    [RequireProjectPermission(ProjectPermission.ReopenTasks)]
    public IActionResult ReopenTasks() => Ok();

    [HttpGet(nameof(ProjectPermission.Comment))]
    [RequireProjectPermission(ProjectPermission.Comment)]
    public IActionResult Comment() => Ok();

    [HttpGet(nameof(ProjectPermission.ViewActivity))]
    [RequireProjectPermission(ProjectPermission.ViewActivity)]
    public IActionResult ViewActivity() => Ok();
}
