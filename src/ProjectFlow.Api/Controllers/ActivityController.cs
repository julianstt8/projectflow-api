using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Activity;
using ProjectFlow.Application.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>The project's immutable activity log (RF-10).</summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/activity")]
public sealed class ActivityController(ISender sender) : ApiControllerBase
{
    /// <summary>
    /// Who changed what and when, newest first. Use <c>entityId</c> for the history of one task, sprint or epic.
    /// Organization admins and project managers only.
    /// </summary>
    [HttpGet]
    [RequireProjectPermission(ProjectPermission.ViewActivity)]
    [ProducesResponseType<PagedResponse<ActivityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        Guid organizationId,
        Guid projectId,
        [FromQuery] Guid? entityId,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        Ok(await sender.Send(new GetActivityQuery(projectId, entityId, page, pageSize), cancellationToken));
}
