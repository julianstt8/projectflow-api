using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    public static readonly Guid OrganizationId = Guid.NewGuid();

    public static Project CreateProject(string key = "PRJ", Guid? creatorUserId = null) =>
        Project.Create(OrganizationId, ProjectKey.Create(key).Value, "ProjectFlow", null, creatorUserId ?? Guid.NewGuid(), Now).Value;

    public static TaskItem CreateTask(Project project) =>
        TaskItem.Create(project, Guid.NewGuid(), TaskType.Story, "Implement login", null, TaskPriority.Medium, Now).Value;

    /// <summary>Walks a new task along the allowed workflow until it reaches <paramref name="target"/>.</summary>
    public static TaskItem CreateTaskIn(Project project, TaskItemStatus target)
    {
        var task = CreateTask(project);
        TaskItemStatus[] path = [TaskItemStatus.InProgress, TaskItemStatus.Review, TaskItemStatus.Done];

        foreach (var status in path.TakeWhile(_ => task.Status != target))
        {
            task.ChangeStatus(status, Now);
        }

        return task;
    }
}
