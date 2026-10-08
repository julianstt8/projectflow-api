using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Labels;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>Labels of a project and the labels of its tasks (RF-09).</summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}")]
public sealed class LabelsController(ISender sender) : ApiControllerBase
{
    /// <summary>Labels of the project, by name.</summary>
    [HttpGet("labels")]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<IReadOnlyList<LabelResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetLabelsQuery(projectId), cancellationToken));

    /// <summary>Creates a label (hex color like <c>#1D76DB</c>). Names are unique per project, ignoring case.</summary>
    [HttpPost("labels")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType<LabelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid organizationId, Guid projectId, LabelRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateLabelCommand(projectId, request.Name, request.Color), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Renames a label or changes its color.</summary>
    [HttpPut("labels/{labelId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Update(Guid organizationId, Guid projectId, Guid labelId, LabelRequest request, CancellationToken cancellationToken) =>
        SendAsync(new UpdateLabelCommand(projectId, labelId, request.Name, request.Color), cancellationToken);

    /// <summary>Deletes the label and removes it from every task.</summary>
    [HttpDelete("labels/{labelId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Delete(Guid organizationId, Guid projectId, Guid labelId, CancellationToken cancellationToken) =>
        SendAsync(new DeleteLabelCommand(projectId, labelId), cancellationToken);

    /// <summary>Tags a task with a label of its project (idempotent).</summary>
    [HttpPut("tasks/{taskId:guid}/labels/{labelId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> AddToTask(Guid organizationId, Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken) =>
        SendAsync(new AddTaskLabelCommand(organizationId, projectId, taskId, labelId), cancellationToken);

    /// <summary>Removes a label from a task (idempotent).</summary>
    [HttpDelete("tasks/{taskId:guid}/labels/{labelId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoveFromTask(Guid organizationId, Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveTaskLabelCommand(organizationId, projectId, taskId, labelId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

/// <summary>Data of a label.</summary>
/// <param name="Name">Label name, unique in the project ignoring case, up to 50 characters.</param>
/// <param name="Color">Hex color such as <c>#1D76DB</c>.</param>
public sealed record LabelRequest(string Name, string Color);
