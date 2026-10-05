using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Comments;

public static class CommentErrors
{
    public static readonly Error BodyRequired =
        Error.Validation("Comment.BodyRequired", "The comment cannot be empty.");

    public static readonly Error BodyTooLong =
        Error.Validation("Comment.BodyTooLong", $"The comment cannot exceed {Comment.BodyMaxLength} characters.");

    public static readonly Error AuthorRequired =
        Error.Validation("Comment.AuthorRequired", "A valid author id is required.");

    public static readonly Error NotAuthor =
        Error.Forbidden("Comment.NotAuthor", "Only the author can edit a comment.");
}
