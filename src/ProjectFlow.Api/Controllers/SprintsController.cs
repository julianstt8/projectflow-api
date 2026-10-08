using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>Sprints of a project (RF-05: at most one active sprint per project).</summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/sprints")]
public sealed class SprintsController(ISender sender) : ApiControllerBase
{
    /// <summary>Sprints of the project with their task count, dated ones first.</summary>
    [HttpGet]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<IReadOnlyList<SprintResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSprintsQuery(projectId), cancellationToken));

    /// <summary>A sprint with its task count.</summary>
    [HttpGet("{sprintId:guid}")]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, Guid projectId, Guid sprintId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSprintQuery(projectId, sprintId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Plans a new sprint. Dates are optional; the start date defaults to the day it starts.</summary>
    [HttpPost]
    [RequireProjectPermission(ProjectPermission.ManageSprints)]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid organizationId, Guid projectId, SprintRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSprintCommand(projectId, request.Name, request.Goal, request.StartDate, request.EndDate),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { organizationId, projectId, sprintId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Changes name, goal or dates. Completed sprints cannot change (422).</summary>
    [HttpPut("{sprintId:guid}")]
    [RequireProjectPermission(ProjectPermission.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Update(Guid organizationId, Guid projectId, Guid sprintId, SprintRequest request, CancellationToken cancellationToken) =>
        SendAsync(new UpdateSprintCommand(projectId, sprintId, request.Name, request.Goal, request.StartDate, request.EndDate), cancellationToken);

    /// <summary>Starts a planned sprint. Fails with 409 if the project already has an active sprint.</summary>
    [HttpPost("{sprintId:guid}/start")]
    [RequireProjectPermission(ProjectPermission.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Start(Guid organizationId, Guid projectId, Guid sprintId, CancellationToken cancellationToken) =>
        SendAsync(new StartSprintCommand(projectId, sprintId), cancellationToken);

    /// <summary>Completes the active sprint; unfinished tasks go back to the backlog.</summary>
    [HttpPost("{sprintId:guid}/complete")]
    [RequireProjectPermission(ProjectPermission.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Complete(Guid organizationId, Guid projectId, Guid sprintId, CancellationToken cancellationToken) =>
        SendAsync(new CompleteSprintCommand(projectId, sprintId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

public sealed record SprintRequest(string Name, string? Goal, DateOnly? StartDate, DateOnly? EndDate);
