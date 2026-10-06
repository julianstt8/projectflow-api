using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Comments;
using ProjectFlow.Application.Labels;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

internal sealed class CommentQueries(ApplicationDbContext dbContext) : ICommentQueries
{
    public async Task<IReadOnlyList<CommentResponse>> ListByTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.TaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Join(
                dbContext.Users,
                comment => comment.AuthorId,
                user => user.Id,
                (comment, user) => new CommentResponse(comment.Id, comment.TaskId, comment.AuthorId, user.FullName, comment.Body, comment.CreatedAt))
            .ToListAsync(cancellationToken);
}

internal sealed class LabelQueries(ApplicationDbContext dbContext) : ILabelQueries
{
    public async Task<IReadOnlyList<LabelResponse>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Labels
            .AsNoTracking()
            .Where(label => label.ProjectId == projectId)
            .OrderBy(label => label.Name)
            .Select(label => new LabelResponse(label.Id, label.Name, label.Color))
            .ToListAsync(cancellationToken);
}
