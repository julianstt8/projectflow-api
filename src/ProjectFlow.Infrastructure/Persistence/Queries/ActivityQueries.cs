using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Activity;
using ProjectFlow.Application.Common;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

internal sealed class ActivityQueries(ApplicationDbContext dbContext) : IActivityQueries
{
    public async Task<PagedResponse<ActivityResponse>> ListAsync(
        Guid projectId,
        Guid? entityId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var logs = dbContext.ActivityLogs
            .AsNoTracking()
            .Where(log => log.ProjectId == projectId && (entityId == null || log.EntityId == entityId));

        var totalCount = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                dbContext.Users,
                log => log.ActorId,
                user => user.Id,
                (log, user) => new ActivityResponse(
                    log.Id,
                    log.ActorId,
                    user.FullName,
                    log.EntityType,
                    log.EntityId,
                    log.Action,
                    log.OldValue,
                    log.NewValue,
                    log.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ActivityResponse>(items, page, pageSize, totalCount);
    }
}
