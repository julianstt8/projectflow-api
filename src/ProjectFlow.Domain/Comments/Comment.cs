using ProjectFlow.Domain.Activity;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Domain.Comments;

public sealed class Comment : Entity, IOrganizationOwned
{
    public const int BodyMaxLength = 5000;

    private Comment(Guid id, Guid taskId, Guid organizationId, Guid authorId, string body, DateTimeOffset createdAt)
        : base(id)
    {
        TaskId = taskId;
        OrganizationId = organizationId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid TaskId { get; }

    public Guid OrganizationId { get; }

    public Guid AuthorId { get; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Adds a comment to a task (RF-09). Done tasks can still be commented on.</summary>
    public static Result<Comment> Create(TaskItem task, Guid authorId, string body, DateTimeOffset now)
    {
        if (task.IsDeleted)
        {
            return TaskErrors.Deleted;
        }

        if (authorId == Guid.Empty)
        {
            return CommentErrors.AuthorRequired;
        }

        var validBody = Text.Required(body, BodyMaxLength, CommentErrors.BodyRequired, CommentErrors.BodyTooLong);
        if (validBody.IsFailure)
        {
            return validBody.Error;
        }

        var comment = new Comment(Guid.CreateVersion7(now), task.Id, task.OrganizationId, authorId, validBody.Value, now);
        comment.Raise(new CommentAdded(task.OrganizationId, task.ProjectId, task.Id, comment.Id));
        return comment;
    }

    public Result Edit(Guid editorId, string body)
    {
        if (editorId != AuthorId)
        {
            return CommentErrors.NotAuthor;
        }

        var validBody = Text.Required(body, BodyMaxLength, CommentErrors.BodyRequired, CommentErrors.BodyTooLong);
        if (validBody.IsFailure)
        {
            return validBody.Error;
        }

        Body = validBody.Value;
        return Result.Success();
    }
}
