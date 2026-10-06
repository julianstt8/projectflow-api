using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Application.Tasks;

// ---------- Queries ----------

public sealed record GetTasksQuery(Guid ProjectId) : IQuery<IReadOnlyList<TaskResponse>>;

public sealed class GetTasksQueryHandler(ITaskQueries queries) : IQueryHandler<GetTasksQuery, IReadOnlyList<TaskResponse>>
{
    public async ValueTask<IReadOnlyList<TaskResponse>> Handle(GetTasksQuery query, CancellationToken cancellationToken) =>
        await queries.ListByProjectAsync(query.ProjectId, cancellationToken);
}

public sealed record GetTaskQuery(Guid ProjectId, Guid TaskId) : IQuery<Result<TaskResponse>>;

public sealed class GetTaskQueryHandler(ITaskQueries queries) : IQueryHandler<GetTaskQuery, Result<TaskResponse>>
{
    public async ValueTask<Result<TaskResponse>> Handle(GetTaskQuery query, CancellationToken cancellationToken)
    {
        var task = await queries.GetAsync(query.ProjectId, query.TaskId, cancellationToken);

        return task is null ? TaskUseCaseErrors.NotFound : task;
    }
}

// ---------- Create ----------

public sealed record CreateTaskCommand(
    Guid OrganizationId,
    Guid ProjectId,
    TaskType Type,
    string Title,
    string? Description,
    TaskPriority Priority,
    int? StoryPoints,
    Guid? AssigneeId,
    Guid? SprintId,
    Guid? EpicId) : ICommand<Result<TaskResponse>>;

public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(TaskItem.TitleMaxLength);
        RuleFor(command => command.Description).MaximumLength(TaskItem.DescriptionMaxLength);
        RuleFor(command => command.StoryPoints).InclusiveBetween(0, TaskItem.MaxStoryPoints);
    }
}

/// <summary>
/// Creates a task (RF-06) with the next number of the project (RF-04). The project row is locked for the
/// duration of the transaction, so concurrent creations wait for each other and never share a number.
/// </summary>
public sealed class CreateTaskCommandHandler(
    IProjectRepository projects,
    ITaskRepository tasks,
    ISprintRepository sprints,
    IEpicRepository epics,
    IProjectAccessResolver projectAccess,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateTaskCommand, Result<TaskResponse>>
{
    public async ValueTask<Result<TaskResponse>> Handle(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        if (command.AssigneeId is { } assigneeId
            && !await TaskRules.CanBeAssignedAsync(projectAccess, command.OrganizationId, command.ProjectId, assigneeId, cancellationToken))
        {
            return TaskUseCaseErrors.AssigneeNotAllowed;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await projects.LockForTaskNumberingAsync(command.ProjectId, cancellationToken);

        var created = await CreateAsync(command, cancellationToken);
        if (created.IsFailure)
        {
            return created.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TaskRules.ToResponse(created.Value.Task, created.Value.ProjectKey);
    }

    private async Task<Result<(TaskItem Task, string ProjectKey)>> CreateAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var task = TaskItem.Create(project, currentUser.UserId, command.Type, command.Title, command.Description, command.Priority, now);
        if (task.IsFailure)
        {
            return task.Error;
        }

        var placed = await TaskRules.PlaceAsync(task.Value, sprints, epics, command.SprintId, command.EpicId, now, cancellationToken);
        if (placed.IsFailure)
        {
            return placed.Error;
        }

        var estimated = task.Value.Estimate(command.StoryPoints, now);
        if (estimated.IsFailure)
        {
            return estimated.Error;
        }

        var assigned = task.Value.Assign(command.AssigneeId, now);
        if (assigned.IsFailure)
        {
            return assigned.Error;
        }

        tasks.Add(task.Value);
        return (task.Value, project.Key.Value);
    }
}

// ---------- Edit ----------

public sealed record UpdateTaskCommand(
    Guid OrganizationId,
    Guid ProjectId,
    Guid TaskId,
    TaskType Type,
    string Title,
    string? Description,
    TaskPriority Priority,
    int? StoryPoints) : ICommand<Result>;

public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(TaskItem.TitleMaxLength);
        RuleFor(command => command.Description).MaximumLength(TaskItem.DescriptionMaxLength);
        RuleFor(command => command.StoryPoints).InclusiveBetween(0, TaskItem.MaxStoryPoints);
    }
}

public sealed class UpdateTaskCommandHandler(TaskEditor editor, TimeProvider timeProvider) : ICommandHandler<UpdateTaskCommand, Result>
{
    public ValueTask<Result> Handle(UpdateTaskCommand command, CancellationToken cancellationToken) =>
        editor.EditAsync(command.OrganizationId, command.ProjectId, command.TaskId, task =>
        {
            var now = timeProvider.GetUtcNow();
            var details = task.UpdateDetails(command.Type, command.Title, command.Description, command.Priority, now);
            return details.IsFailure ? details : task.Estimate(command.StoryPoints, now);
        }, cancellationToken);
}

/// <summary>Assigns the task, or unassigns it with a null assignee.</summary>
public sealed record AssignTaskCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid? AssigneeId) : ICommand<Result>;

public sealed class AssignTaskCommandHandler(TaskEditor editor, IProjectAccessResolver projectAccess, TimeProvider timeProvider)
    : ICommandHandler<AssignTaskCommand, Result>
{
    public async ValueTask<Result> Handle(AssignTaskCommand command, CancellationToken cancellationToken)
    {
        if (command.AssigneeId is { } assigneeId
            && !await TaskRules.CanBeAssignedAsync(projectAccess, command.OrganizationId, command.ProjectId, assigneeId, cancellationToken))
        {
            return TaskUseCaseErrors.AssigneeNotAllowed;
        }

        return await editor.EditAsync(
            command.OrganizationId,
            command.ProjectId,
            command.TaskId,
            task => task.Assign(command.AssigneeId, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}

/// <summary>Moves the task into a sprint, or back to the backlog with a null sprint.</summary>
public sealed record MoveTaskToSprintCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid? SprintId) : ICommand<Result>;

public sealed class MoveTaskToSprintCommandHandler(TaskEditor editor, ISprintRepository sprints, TimeProvider timeProvider)
    : ICommandHandler<MoveTaskToSprintCommand, Result>
{
    public async ValueTask<Result> Handle(MoveTaskToSprintCommand command, CancellationToken cancellationToken)
    {
        Sprint? sprint = null;
        if (command.SprintId is { } sprintId && (sprint = await sprints.GetAsync(command.ProjectId, sprintId, cancellationToken)) is null)
        {
            return TaskUseCaseErrors.SprintNotInProject;
        }

        return await editor.EditAsync(
            command.OrganizationId,
            command.ProjectId,
            command.TaskId,
            task => task.MoveToSprint(sprint, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}

/// <summary>Links the task to an epic, or unlinks it with a null epic.</summary>
public sealed record SetTaskEpicCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid? EpicId) : ICommand<Result>;

public sealed class SetTaskEpicCommandHandler(TaskEditor editor, IEpicRepository epics, TimeProvider timeProvider)
    : ICommandHandler<SetTaskEpicCommand, Result>
{
    public async ValueTask<Result> Handle(SetTaskEpicCommand command, CancellationToken cancellationToken)
    {
        Epic? epic = null;
        if (command.EpicId is { } epicId && (epic = await epics.GetAsync(command.ProjectId, epicId, cancellationToken)) is null)
        {
            return TaskUseCaseErrors.EpicNotInProject;
        }

        return await editor.EditAsync(
            command.OrganizationId,
            command.ProjectId,
            command.TaskId,
            task => task.SetEpic(epic, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}

/// <summary>Soft delete. Restricted to project managers and admins by the endpoint permission.</summary>
public sealed record DeleteTaskCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId) : ICommand<Result>;

public sealed class DeleteTaskCommandHandler(TaskEditor editor, TimeProvider timeProvider) : ICommandHandler<DeleteTaskCommand, Result>
{
    public ValueTask<Result> Handle(DeleteTaskCommand command, CancellationToken cancellationToken) =>
        editor.EditAsync(command.OrganizationId, command.ProjectId, command.TaskId, task => task.Delete(timeProvider.GetUtcNow()), cancellationToken);
}

// ---------- Shared ----------

/// <summary>
/// Loads a task for a change and applies the resource-based rule: developers change only tasks they
/// reported or are assigned to (<see cref="ProjectAccess.CanEditTask"/>). Archived projects are read-only.
/// </summary>
public sealed class TaskEditor(
    IProjectRepository projects,
    ITaskRepository tasks,
    IProjectAccessResolver projectAccess,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async ValueTask<Result> EditAsync(
        Guid organizationId,
        Guid projectId,
        Guid taskId,
        Func<TaskItem, Result> change,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var task = await tasks.GetAsync(projectId, taskId, cancellationToken);
        if (task is null)
        {
            return TaskUseCaseErrors.NotFound;
        }

        var access = await projectAccess.GetAsync(organizationId, projectId, currentUser.UserId, cancellationToken);
        if (access is null || !access.CanEditTask(task))
        {
            return TaskUseCaseErrors.NotYourTask;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var result = change(task);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class TaskRules
{
    /// <summary>Only people who can work on tasks (organization admins, project managers, developers) can be assigned.</summary>
    public static async Task<bool> CanBeAssignedAsync(
        IProjectAccessResolver projectAccess,
        Guid organizationId,
        Guid projectId,
        Guid assigneeId,
        CancellationToken cancellationToken)
    {
        var access = await projectAccess.GetAsync(organizationId, projectId, assigneeId, cancellationToken);
        return access?.Has(ProjectPermission.EditOwnTasks) == true;
    }

    public static async Task<Result> PlaceAsync(
        TaskItem task,
        ISprintRepository sprints,
        IEpicRepository epics,
        Guid? sprintId,
        Guid? epicId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (sprintId is { } sid)
        {
            var sprint = await sprints.GetAsync(task.ProjectId, sid, cancellationToken);
            if (sprint is null)
            {
                return TaskUseCaseErrors.SprintNotInProject;
            }

            var moved = task.MoveToSprint(sprint, now);
            if (moved.IsFailure)
            {
                return moved;
            }
        }

        if (epicId is { } eid)
        {
            var epic = await epics.GetAsync(task.ProjectId, eid, cancellationToken);
            if (epic is null)
            {
                return TaskUseCaseErrors.EpicNotInProject;
            }

            return task.SetEpic(epic, now);
        }

        return Result.Success();
    }

    public static TaskResponse ToResponse(TaskItem task, string projectKey) =>
        new(
            task.Id,
            $"{projectKey}-{task.Number}",
            task.Number,
            task.Title,
            task.Description,
            task.Type,
            task.Priority,
            task.Status,
            task.StoryPoints,
            task.ReporterId,
            task.AssigneeId,
            task.SprintId,
            task.EpicId,
            task.Labels.Select(label => label.LabelId).ToList(),
            task.CreatedAt,
            task.UpdatedAt);
}
