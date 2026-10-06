using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Comments;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class CommentRepository(ApplicationDbContext dbContext) : ICommentRepository
{
    public Task<Comment?> GetAsync(Guid taskId, Guid commentId, CancellationToken cancellationToken) =>
        dbContext.Comments.SingleOrDefaultAsync(comment => comment.Id == commentId && comment.TaskId == taskId, cancellationToken);

    public void Add(Comment comment) => dbContext.Comments.Add(comment);

    public void Remove(Comment comment) => dbContext.Comments.Remove(comment);
}
