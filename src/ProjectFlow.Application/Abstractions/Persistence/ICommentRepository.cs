using ProjectFlow.Domain.Comments;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ICommentRepository
{
    /// <summary>The comment, only if it belongs to <paramref name="taskId"/>.</summary>
    Task<Comment?> GetAsync(Guid taskId, Guid commentId, CancellationToken cancellationToken);

    void Add(Comment comment);

    /// <summary>Comments are deleted for good (only projects and tasks are soft-deleted).</summary>
    void Remove(Comment comment);
}
