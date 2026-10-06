using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.Controllers;

/// <summary>
/// Tasks of a project (RF-06). Developers can change only tasks they reported or are assigned to:
/// the route requires <see cref="ProjectPermission.EditOwnTasks"/> and the use case checks the specific task.
/// </summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/tasks")]
public sealed class TasksController(ISender sender) : ApiControllerBase
{
    /// <summary>Tasks of the project by number.</summary>
    [HttpGet]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetTasksQuery(projectId), cancellationToken));

    [HttpGet("{taskId:guid}")]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTaskQuery(projectId, taskId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Creates a task with the next key of the project (e.g. <c>WEB-12</c>). The current user is the reporter.</summary>
    [HttpPost]
    [RequireProjectPermission(ProjectPermission.CreateTasks)]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid organizationId, Guid projectId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateTaskCommand(
                organizationId,
                projectId,
                request.Type,
                request.Title,
                request.Description,
                request.Priority,
                request.StoryPoints,
                request.AssigneeId,
                request.SprintId,
                request.EpicId),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { organizationId, projectId, taskId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Changes type, title, description, priority and story points. Done tasks are read-only (422).</summary>
    [HttpPut("{taskId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Update(Guid organizationId, Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        SendAsync(
            new UpdateTaskCommand(organizationId, projectId, taskId, request.Type, request.Title, request.Description, request.Priority, request.StoryPoints),
            cancellationToken);

    /// <summary>Assigns the task (or unassigns it with <c>null</c>).</summary>
    [HttpPut("{taskId:guid}/assignee")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Assign(Guid organizationId, Guid projectId, Guid taskId, AssignTaskRequest request, CancellationToken cancellationToken) =>
        SendAsync(new AssignTaskCommand(organizationId, projectId, taskId, request.AssigneeId), cancellationToken);

    /// <summary>Moves the task into a sprint (or back to the backlog with <c>null</c>).</summary>
    [HttpPut("{taskId:guid}/sprint")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> MoveToSprint(Guid organizationId, Guid projectId, Guid taskId, MoveTaskToSprintRequest request, CancellationToken cancellationToken) =>
        SendAsync(new MoveTaskToSprintCommand(organizationId, projectId, taskId, request.SprintId), cancellationToken);

    /// <summary>Links the task to an epic (or unlinks it with <c>null</c>).</summary>
    [HttpPut("{taskId:guid}/epic")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> SetEpic(Guid organizationId, Guid projectId, Guid taskId, SetTaskEpicRequest request, CancellationToken cancellationToken) =>
        SendAsync(new SetTaskEpicCommand(organizationId, projectId, taskId, request.EpicId), cancellationToken);

    /// <summary>
    /// Moves the task through the workflow ToDo → InProgress → Review → Done; Review → InProgress is the only
    /// step back (RF-07). Other transitions return 422 <c>Task.InvalidTransition</c>.
    /// </summary>
    [HttpPost("{taskId:guid}/status")]
    [RequireProjectPermission(ProjectPermission.EditOwnTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ChangeStatus(Guid organizationId, Guid projectId, Guid taskId, ChangeTaskStatusRequest request, CancellationToken cancellationToken) =>
        SendAsync(new ChangeTaskStatusCommand(organizationId, projectId, taskId, request.Status), cancellationToken);

    /// <summary>Reopens a done task back to InProgress (RF-08). Project managers and admins only.</summary>
    [HttpPost("{taskId:guid}/reopen")]
    [RequireProjectPermission(ProjectPermission.ReopenTasks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Reopen(Guid organizationId, Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        SendAsync(new ReopenTaskCommand(organizationId, projectId, taskId), cancellationToken);

    /// <summary>Soft-deletes the task. Project managers and admins only.</summary>
    [HttpDelete("{taskId:guid}")]
    [RequireProjectPermission(ProjectPermission.EditAnyTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(Guid organizationId, Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        SendAsync(new DeleteTaskCommand(organizationId, projectId, taskId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

public sealed record CreateTaskRequest(
    TaskType Type,
    string Title,
    string? Description,
    TaskPriority Priority,
    int? StoryPoints,
    Guid? AssigneeId,
    Guid? SprintId,
    Guid? EpicId);

public sealed record UpdateTaskRequest(TaskType Type, string Title, string? Description, TaskPriority Priority, int? StoryPoints);

public sealed record AssignTaskRequest(Guid? AssigneeId);

public sealed record MoveTaskToSprintRequest(Guid? SprintId);

public sealed record SetTaskEpicRequest(Guid? EpicId);

public sealed record ChangeTaskStatusRequest(TaskItemStatus Status);
