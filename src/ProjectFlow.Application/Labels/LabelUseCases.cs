using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Labels;

public sealed record LabelResponse(Guid Id, string Name, string Color);

public static class LabelUseCaseErrors
{
    public static readonly Error NotFound = Error.NotFound("Label.NotFound", "The label does not exist.");

    public static readonly Error NameTaken = Error.Conflict("Label.NameTaken", "The project already has a label with this name.");

    public static readonly Error NotInProject = Error.BusinessRule("Label.NotInProject", "The label does not exist in this project.");
}

// ---------- Project labels ----------

public sealed record GetLabelsQuery(Guid ProjectId) : IQuery<IReadOnlyList<LabelResponse>>;

public sealed class GetLabelsQueryHandler(ILabelQueries queries) : IQueryHandler<GetLabelsQuery, IReadOnlyList<LabelResponse>>
{
    public async ValueTask<IReadOnlyList<LabelResponse>> Handle(GetLabelsQuery query, CancellationToken cancellationToken) =>
        await queries.ListByProjectAsync(query.ProjectId, cancellationToken);
}

public sealed record CreateLabelCommand(Guid ProjectId, string Name, string Color) : ICommand<Result<LabelResponse>>;

public sealed class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Label.NameMaxLength);
        RuleFor(command => command.Color).NotEmpty();
    }
}

public sealed class CreateLabelCommandHandler(
    IProjectRepository projects,
    ILabelRepository labels,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateLabelCommand, Result<LabelResponse>>
{
    public async ValueTask<Result<LabelResponse>> Handle(CreateLabelCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var label = Label.Create(project, command.Name, command.Color, timeProvider.GetUtcNow());
        if (label.IsFailure)
        {
            return label.Error;
        }

        if (await labels.ExistsWithNameAsync(command.ProjectId, label.Value.Name, exceptLabelId: null, cancellationToken))
        {
            return LabelUseCaseErrors.NameTaken;
        }

        labels.Add(label.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LabelResponse(label.Value.Id, label.Value.Name, label.Value.Color);
    }
}

public sealed record UpdateLabelCommand(Guid ProjectId, Guid LabelId, string Name, string Color) : ICommand<Result>;

public sealed class UpdateLabelCommandValidator : AbstractValidator<UpdateLabelCommand>
{
    public UpdateLabelCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Label.NameMaxLength);
        RuleFor(command => command.Color).NotEmpty();
    }
}

public sealed class UpdateLabelCommandHandler(IProjectRepository projects, ILabelRepository labels, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLabelCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateLabelCommand command, CancellationToken cancellationToken)
    {
        var target = await LabelTarget.LoadAsync(projects, labels, command.ProjectId, command.LabelId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var updated = target.Value.Update(command.Name, command.Color);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (await labels.ExistsWithNameAsync(command.ProjectId, target.Value.Name, command.LabelId, cancellationToken))
        {
            return LabelUseCaseErrors.NameTaken;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteLabelCommand(Guid ProjectId, Guid LabelId) : ICommand<Result>;

/// <summary>Deletes the label for good and removes it from every task.</summary>
public sealed class DeleteLabelCommandHandler(IProjectRepository projects, ILabelRepository labels, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteLabelCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteLabelCommand command, CancellationToken cancellationToken)
    {
        var target = await LabelTarget.LoadAsync(projects, labels, command.ProjectId, command.LabelId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        labels.Remove(target.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---------- Labels on tasks ----------

public sealed record AddTaskLabelCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid LabelId) : ICommand<Result>;

/// <summary>Tags a task (idempotent). Same rules as any task change: developers only on their own or assigned tasks.</summary>
public sealed class AddTaskLabelCommandHandler(TaskEditor editor, ILabelRepository labels, TimeProvider timeProvider)
    : ICommandHandler<AddTaskLabelCommand, Result>
{
    public async ValueTask<Result> Handle(AddTaskLabelCommand command, CancellationToken cancellationToken)
    {
        var label = await labels.GetAsync(command.ProjectId, command.LabelId, cancellationToken);
        if (label is null)
        {
            return LabelUseCaseErrors.NotInProject;
        }

        return await editor.EditAsync(
            command.OrganizationId,
            command.ProjectId,
            command.TaskId,
            task => task.AddLabel(label, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}

public sealed record RemoveTaskLabelCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid LabelId) : ICommand<Result>;

public sealed class RemoveTaskLabelCommandHandler(TaskEditor editor, TimeProvider timeProvider)
    : ICommandHandler<RemoveTaskLabelCommand, Result>
{
    public ValueTask<Result> Handle(RemoveTaskLabelCommand command, CancellationToken cancellationToken) =>
        editor.EditAsync(
            command.OrganizationId,
            command.ProjectId,
            command.TaskId,
            task => task.RemoveLabel(command.LabelId, timeProvider.GetUtcNow()),
            cancellationToken);
}

internal static class LabelTarget
{
    public static async Task<Result<Label>> LoadAsync(
        IProjectRepository projects,
        ILabelRepository labels,
        Guid projectId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var label = await labels.GetAsync(projectId, labelId, cancellationToken);
        if (label is null)
        {
            return LabelUseCaseErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        return label;
    }
}
