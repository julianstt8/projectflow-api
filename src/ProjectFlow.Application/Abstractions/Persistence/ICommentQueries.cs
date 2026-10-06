using ProjectFlow.Application.Comments;
using ProjectFlow.Application.Labels;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ICommentQueries
{
    /// <summary>Comments of the task, oldest first, with the author's name.</summary>
    Task<IReadOnlyList<CommentResponse>> ListByTaskAsync(Guid taskId, CancellationToken cancellationToken);
}

public interface ILabelQueries
{
    Task<IReadOnlyList<LabelResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}
