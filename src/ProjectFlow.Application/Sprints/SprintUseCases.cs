using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Application.Sprints;

public sealed record SprintResponse(
    Guid Id,
    string Name,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    SprintStatus Status,
    int TaskCount);

public static class SprintUseCaseErrors
{
    public static readonly Error NotFound = Error.NotFound("Sprint.NotFound", "The sprint does not exist.");
}

// ---------- Queries ----------

public sealed record GetSprintsQuery(Guid ProjectId) : IQuery<IReadOnlyList<SprintResponse>>;

public sealed class GetSprintsQueryHandler(ISprintQueries queries) : IQueryHandler<GetSprintsQuery, IReadOnlyList<SprintResponse>>
{
    public async ValueTask<IReadOnlyList<SprintResponse>> Handle(GetSprintsQuery query, CancellationToken cancellationToken) =>
        await queries.ListByProjectAsync(query.ProjectId, cancellationToken);
}

public sealed record GetSprintQuery(Guid ProjectId, Guid SprintId) : IQuery<Result<SprintResponse>>;

public sealed class GetSprintQueryHandler(ISprintQueries queries) : IQueryHandler<GetSprintQuery, Result<SprintResponse>>
{
    public async ValueTask<Result<SprintResponse>> Handle(GetSprintQuery query, CancellationToken cancellationToken)
    {
        var sprint = await queries.GetAsync(query.ProjectId, query.SprintId, cancellationToken);

        return sprint is null ? SprintUseCaseErrors.NotFound : sprint;
    }
}

// ---------- Create and update ----------

public sealed record CreateSprintCommand(Guid ProjectId, string Name, string? Goal, DateOnly? StartDate, DateOnly? EndDate)
    : ICommand<Result<SprintResponse>>;

public sealed class CreateSprintCommandValidator : AbstractValidator<CreateSprintCommand>
{
    public CreateSprintCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Sprint.NameMaxLength);
        RuleFor(command => command.Goal).MaximumLength(Sprint.GoalMaxLength);
    }
}

public sealed class CreateSprintCommandHandler(
    IProjectRepository projects,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateSprintCommand, Result<SprintResponse>>
{
    public async ValueTask<Result<SprintResponse>> Handle(CreateSprintCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var sprint = Sprint.Create(project, command.Name, command.Goal, command.StartDate, command.EndDate, timeProvider.GetUtcNow());
        if (sprint.IsFailure)
        {
            return sprint.Error;
        }

        sprints.Add(sprint.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sprint.Value.ToResponse(taskCount: 0);
    }
}

public sealed record UpdateSprintCommand(Guid ProjectId, Guid SprintId, string Name, string? Goal, DateOnly? StartDate, DateOnly? EndDate)
    : ICommand<Result>;

public sealed class UpdateSprintCommandValidator : AbstractValidator<UpdateSprintCommand>
{
    public UpdateSprintCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Sprint.NameMaxLength);
        RuleFor(command => command.Goal).MaximumLength(Sprint.GoalMaxLength);
    }
}

public sealed class UpdateSprintCommandHandler(IProjectRepository projects, ISprintRepository sprints, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSprintCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateSprintCommand command, CancellationToken cancellationToken)
    {
        var target = await SprintTarget.LoadAsync(projects, sprints, command.ProjectId, command.SprintId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var updated = target.Value.UpdateDetails(command.Name, command.Goal, command.StartDate, command.EndDate);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---------- Start and complete ----------

public sealed record StartSprintCommand(Guid ProjectId, Guid SprintId) : ICommand<Result>;

/// <summary>Starts a planned sprint. A project has at most one active sprint (RF-05).</summary>
public sealed class StartSprintCommandHandler(
    IProjectRepository projects,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<StartSprintCommand, Result>
{
    public async ValueTask<Result> Handle(StartSprintCommand command, CancellationToken cancellationToken)
    {
        var target = await SprintTarget.LoadAsync(projects, sprints, command.ProjectId, command.SprintId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var projectSprints = await sprints.ListByProjectAsync(command.ProjectId, cancellationToken);
        var started = target.Value.Start(projectSprints, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        if (started.IsFailure)
        {
            return started;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another request started a sprint at the same time: the partial unique index stopped this one.
            return SprintErrors.AnotherSprintActive;
        }

        return Result.Success();
    }
}

public sealed record CompleteSprintCommand(Guid ProjectId, Guid SprintId) : ICommand<Result>;

/// <summary>Completes the active sprint; its unfinished tasks go back to the backlog, done tasks stay in the sprint.</summary>
public sealed class CompleteSprintCommandHandler(
    IProjectRepository projects,
    ISprintRepository sprints,
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CompleteSprintCommand, Result>
{
    public async ValueTask<Result> Handle(CompleteSprintCommand command, CancellationToken cancellationToken)
    {
        var target = await SprintTarget.LoadAsync(projects, sprints, command.ProjectId, command.SprintId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var now = timeProvider.GetUtcNow();
        var completed = target.Value.Complete(DateOnly.FromDateTime(now.UtcDateTime));
        if (completed.IsFailure)
        {
            return completed;
        }

        foreach (var task in await tasks.ListUnfinishedInSprintAsync(command.SprintId, cancellationToken))
        {
            var moved = task.MoveToSprint(null, now);
            if (moved.IsFailure)
            {
                return moved;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---------- Shared ----------

internal static class SprintTarget
{
    /// <summary>Loads a sprint of a visible project that is not archived (archived projects are read-only).</summary>
    public static async Task<Result<Sprint>> LoadAsync(
        IProjectRepository projects,
        ISprintRepository sprints,
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var sprint = await sprints.GetAsync(projectId, sprintId, cancellationToken);
        if (sprint is null)
        {
            return SprintUseCaseErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        return sprint;
    }

    public static SprintResponse ToResponse(this Sprint sprint, int taskCount) =>
        new(sprint.Id, sprint.Name, sprint.Goal, sprint.StartDate, sprint.EndDate, sprint.Status, taskCount);
}
