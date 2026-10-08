using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Epics;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>Epics group the tasks of a project.</summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/epics")]
public sealed class EpicsController(ISender sender) : ApiControllerBase
{
    /// <summary>Epics of the project with their progress (open first).</summary>
    [HttpGet]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<IReadOnlyList<EpicResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetEpicsQuery(projectId), cancellationToken));

    /// <summary>An epic with its progress.</summary>
    [HttpGet("{epicId:guid}")]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<EpicResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, Guid projectId, Guid epicId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEpicQuery(projectId, epicId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Creates an epic. Project managers and admins only.</summary>
    [HttpPost]
    [RequireProjectPermission(ProjectPermission.ManageEpics)]
    [ProducesResponseType<EpicResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid organizationId, Guid projectId, EpicRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateEpicCommand(projectId, request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { organizationId, projectId, epicId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Renames an epic or changes its description.</summary>
    [HttpPut("{epicId:guid}")]
    [RequireProjectPermission(ProjectPermission.ManageEpics)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Update(Guid organizationId, Guid projectId, Guid epicId, EpicRequest request, CancellationToken cancellationToken) =>
        SendAsync(new UpdateEpicCommand(projectId, epicId, request.Name, request.Description), cancellationToken);

    /// <summary>Closes the epic: no new tasks can be linked to it.</summary>
    [HttpPost("{epicId:guid}/close")]
    [RequireProjectPermission(ProjectPermission.ManageEpics)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Close(Guid organizationId, Guid projectId, Guid epicId, CancellationToken cancellationToken) =>
        SendAsync(new CloseEpicCommand(projectId, epicId), cancellationToken);

    /// <summary>Reopens a closed epic so tasks can be linked to it again.</summary>
    [HttpPost("{epicId:guid}/reopen")]
    [RequireProjectPermission(ProjectPermission.ManageEpics)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Reopen(Guid organizationId, Guid projectId, Guid epicId, CancellationToken cancellationToken) =>
        SendAsync(new ReopenEpicCommand(projectId, epicId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

/// <summary>Data of an epic.</summary>
/// <param name="Name">Epic name, up to 200 characters.</param>
/// <param name="Description">Optional description, up to 5000 characters.</param>
public sealed record EpicRequest(string Name, string? Description);
