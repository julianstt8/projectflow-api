using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class TaskRepository(ApplicationDbContext dbContext) : ITaskRepository
{
    public Task<TaskItem?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Tasks
            .Include(task => task.Labels)
            .SingleOrDefaultAsync(task => task.Id == taskId && task.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> ListUnfinishedInSprintAsync(Guid sprintId, CancellationToken cancellationToken) =>
        await dbContext.Tasks
            .Where(task => task.SprintId == sprintId && task.Status != TaskItemStatus.Done)
            .ToListAsync(cancellationToken);

    public void Add(TaskItem task) => dbContext.Tasks.Add(task);
}
