using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Epics;

/// <param name="TaskCount">Tasks linked to the epic (deleted ones excluded).</param>
/// <param name="DoneTaskCount">Of those, how many are done: the epic's progress.</param>
public sealed record EpicResponse(Guid Id, string Name, string? Description, EpicStatus Status, int TaskCount, int DoneTaskCount);

public static class EpicUseCaseErrors
{
    public static readonly Error NotFound = Error.NotFound("Epic.NotFound", "The epic does not exist.");
}

// ---------- Queries ----------

public sealed record GetEpicsQuery(Guid ProjectId) : IQuery<IReadOnlyList<EpicResponse>>;

public sealed class GetEpicsQueryHandler(IEpicQueries queries) : IQueryHandler<GetEpicsQuery, IReadOnlyList<EpicResponse>>
{
    public async ValueTask<IReadOnlyList<EpicResponse>> Handle(GetEpicsQuery query, CancellationToken cancellationToken) =>
        await queries.ListByProjectAsync(query.ProjectId, cancellationToken);
}

public sealed record GetEpicQuery(Guid ProjectId, Guid EpicId) : IQuery<Result<EpicResponse>>;

public sealed class GetEpicQueryHandler(IEpicQueries queries) : IQueryHandler<GetEpicQuery, Result<EpicResponse>>
{
    public async ValueTask<Result<EpicResponse>> Handle(GetEpicQuery query, CancellationToken cancellationToken)
    {
        var epic = await queries.GetAsync(query.ProjectId, query.EpicId, cancellationToken);

        return epic is null ? EpicUseCaseErrors.NotFound : epic;
    }
}

// ---------- Commands ----------

public sealed record CreateEpicCommand(Guid ProjectId, string Name, string? Description) : ICommand<Result<EpicResponse>>;

public sealed class CreateEpicCommandValidator : AbstractValidator<CreateEpicCommand>
{
    public CreateEpicCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Epic.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Epic.DescriptionMaxLength);
    }
}

public sealed class CreateEpicCommandHandler(
    IProjectRepository projects,
    IEpicRepository epics,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateEpicCommand, Result<EpicResponse>>
{
    public async ValueTask<Result<EpicResponse>> Handle(CreateEpicCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var epic = Epic.Create(project, command.Name, command.Description, timeProvider.GetUtcNow());
        if (epic.IsFailure)
        {
            return epic.Error;
        }

        epics.Add(epic.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EpicResponse(epic.Value.Id, epic.Value.Name, epic.Value.Description, epic.Value.Status, TaskCount: 0, DoneTaskCount: 0);
    }
}

public sealed record UpdateEpicCommand(Guid ProjectId, Guid EpicId, string Name, string? Description) : ICommand<Result>;

public sealed class UpdateEpicCommandValidator : AbstractValidator<UpdateEpicCommand>
{
    public UpdateEpicCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Epic.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Epic.DescriptionMaxLength);
    }
}

public sealed class UpdateEpicCommandHandler(IProjectRepository projects, IEpicRepository epics, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateEpicCommand, Result>
{
    public ValueTask<Result> Handle(UpdateEpicCommand command, CancellationToken cancellationToken) =>
        EpicChange.ApplyAsync(projects, epics, unitOfWork, command.ProjectId, command.EpicId, epic => epic.UpdateDetails(command.Name, command.Description), cancellationToken);
}

/// <summary>Closes an epic: no new tasks can be linked to it (tasks already linked stay).</summary>
public sealed record CloseEpicCommand(Guid ProjectId, Guid EpicId) : ICommand<Result>;

public sealed class CloseEpicCommandHandler(IProjectRepository projects, IEpicRepository epics, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseEpicCommand, Result>
{
    public ValueTask<Result> Handle(CloseEpicCommand command, CancellationToken cancellationToken) =>
        EpicChange.ApplyAsync(projects, epics, unitOfWork, command.ProjectId, command.EpicId, epic => epic.Close(), cancellationToken);
}

public sealed record ReopenEpicCommand(Guid ProjectId, Guid EpicId) : ICommand<Result>;

public sealed class ReopenEpicCommandHandler(IProjectRepository projects, IEpicRepository epics, IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenEpicCommand, Result>
{
    public ValueTask<Result> Handle(ReopenEpicCommand command, CancellationToken cancellationToken) =>
        EpicChange.ApplyAsync(projects, epics, unitOfWork, command.ProjectId, command.EpicId, epic => epic.Reopen(), cancellationToken);
}

internal static class EpicChange
{
    /// <summary>Loads an epic of a visible, non-archived project, applies a domain change and saves it.</summary>
    public static async ValueTask<Result> ApplyAsync(
        IProjectRepository projects,
        IEpicRepository epics,
        IUnitOfWork unitOfWork,
        Guid projectId,
        Guid epicId,
        Func<Epic, Result> change,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var epic = await epics.GetAsync(projectId, epicId, cancellationToken);
        if (epic is null)
        {
            return EpicUseCaseErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var result = change(epic);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
