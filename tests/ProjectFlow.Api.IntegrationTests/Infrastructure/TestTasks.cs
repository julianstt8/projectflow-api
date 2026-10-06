using Microsoft.EntityFrameworkCore;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Infrastructure;

/// <summary>Creates tasks through the domain, straight in the database (used before and besides the task endpoints).</summary>
public static class TestTasks
{
    /// <summary>Saves a task in the project, optionally linked to an epic, moved to a status and soft-deleted.</summary>
    public static async Task<Guid> AddTaskAsync(
        this ProjectFlowApiFactory api,
        Guid organizationId,
        Guid projectId,
        Guid? epicId = null,
        TaskItemStatus status = TaskItemStatus.ToDo,
        bool deleted = false)
    {
        await using var dbContext = api.CreateDbContext(organizationId);
        var project = await dbContext.Projects.Include(p => p.Members).SingleAsync(p => p.Id == projectId);
        var now = DateTimeOffset.UtcNow;
        var task = TaskItem.Create(project, project.Members.First().UserId, TaskType.Task, $"Task {TestData.Unique()}", null, TaskPriority.Medium, now).Value;

        if (epicId is { } id)
        {
            task.SetEpic(await dbContext.Epics.SingleAsync(epic => epic.Id == id), now);
        }

        TaskItemStatus[] workflow = [TaskItemStatus.InProgress, TaskItemStatus.Review, TaskItemStatus.Done];
        foreach (var next in workflow.TakeWhile(_ => task.Status != status))
        {
            task.ChangeStatus(next, now);
        }

        if (deleted)
        {
            task.Delete(now);
        }

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync();
        return task.Id;
    }
}
