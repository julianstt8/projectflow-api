using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Comments;

public sealed record CommentResponse(Guid Id, Guid TaskId, Guid AuthorId, string AuthorName, string Body, DateTimeOffset CreatedAt);

public static class CommentUseCaseErrors
{
    public static readonly Error NotFound = Error.NotFound("Comment.NotFound", "The comment does not exist.");

    public static readonly Error CannotDelete =
        Error.Forbidden("Comment.CannotDelete", "Only the author or a project manager can delete a comment.");
}

// ---------- Query ----------

public sealed record GetCommentsQuery(Guid ProjectId, Guid TaskId) : IQuery<Result<IReadOnlyList<CommentResponse>>>;

public sealed class GetCommentsQueryHandler(ITaskRepository tasks, ICommentQueries queries)
    : IQueryHandler<GetCommentsQuery, Result<IReadOnlyList<CommentResponse>>>
{
    public async ValueTask<Result<IReadOnlyList<CommentResponse>>> Handle(GetCommentsQuery query, CancellationToken cancellationToken)
    {
        if (await tasks.GetAsync(query.ProjectId, query.TaskId, cancellationToken) is null)
        {
            return TaskUseCaseErrors.NotFound;
        }

        return Result.Success(await queries.ListByTaskAsync(query.TaskId, cancellationToken));
    }
}

// ---------- Add ----------

public sealed record AddCommentCommand(Guid ProjectId, Guid TaskId, string Body) : ICommand<Result<CommentResponse>>;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(Comment.BodyMaxLength);
    }
}

/// <summary>Comments on a task (RF-09). Done tasks can still be commented on; archived projects cannot.</summary>
public sealed class AddCommentCommandHandler(
    IProjectRepository projects,
    ITaskRepository tasks,
    ICommentRepository comments,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<AddCommentCommand, Result<CommentResponse>>
{
    public async ValueTask<Result<CommentResponse>> Handle(AddCommentCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var task = await tasks.GetAsync(command.ProjectId, command.TaskId, cancellationToken);
        if (task is null)
        {
            return TaskUseCaseErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var comment = Comment.Create(task, currentUser.UserId, command.Body, timeProvider.GetUtcNow());
        if (comment.IsFailure)
        {
            return comment.Error;
        }

        comments.Add(comment.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var author = await users.GetByIdAsync(currentUser.UserId, cancellationToken);
        return new CommentResponse(
            comment.Value.Id,
            task.Id,
            currentUser.UserId,
            author?.FullName ?? string.Empty,
            comment.Value.Body,
            comment.Value.CreatedAt);
    }
}

// ---------- Edit ----------

public sealed record EditCommentCommand(Guid ProjectId, Guid TaskId, Guid CommentId, string Body) : ICommand<Result>;

public sealed class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(Comment.BodyMaxLength);
    }
}

/// <summary>Only the author edits a comment (domain rule, 403).</summary>
public sealed class EditCommentCommandHandler(
    IProjectRepository projects,
    ITaskRepository tasks,
    ICommentRepository comments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<EditCommentCommand, Result>
{
    public async ValueTask<Result> Handle(EditCommentCommand command, CancellationToken cancellationToken)
    {
        var target = await CommentTarget.LoadAsync(projects, tasks, comments, command.ProjectId, command.TaskId, command.CommentId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var edited = target.Value.Edit(currentUser.UserId, command.Body);
        if (edited.IsFailure)
        {
            return edited;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---------- Delete ----------

public sealed record DeleteCommentCommand(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid CommentId) : ICommand<Result>;

/// <summary>The author deletes their comment; project managers and admins can delete any (moderation).</summary>
public sealed class DeleteCommentCommandHandler(
    IProjectRepository projects,
    ITaskRepository tasks,
    ICommentRepository comments,
    IProjectAccessResolver projectAccess,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<DeleteCommentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        var target = await CommentTarget.LoadAsync(projects, tasks, comments, command.ProjectId, command.TaskId, command.CommentId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var comment = target.Value;
        if (comment.AuthorId != currentUser.UserId)
        {
            var access = await projectAccess.GetAsync(command.OrganizationId, command.ProjectId, currentUser.UserId, cancellationToken);
            if (access?.Has(ProjectPermission.EditAnyTask) != true)
            {
                return CommentUseCaseErrors.CannotDelete;
            }
        }

        comments.Remove(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class CommentTarget
{
    public static async Task<Result<Comment>> LoadAsync(
        IProjectRepository projects,
        ITaskRepository tasks,
        ICommentRepository comments,
        Guid projectId,
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        if (await tasks.GetAsync(projectId, taskId, cancellationToken) is null)
        {
            return TaskUseCaseErrors.NotFound;
        }

        var comment = await comments.GetAsync(taskId, commentId, cancellationToken);
        if (comment is null)
        {
            return CommentUseCaseErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        return comment;
    }
}
