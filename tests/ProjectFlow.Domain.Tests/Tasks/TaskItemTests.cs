using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Tasks;

public class TaskItemTests
{
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    [Fact]
    public void Create_numbers_tasks_sequentially_per_project()
    {
        var project = CreateProject();

        var first = CreateTask(project);
        var second = CreateTask(project);

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Equal("PRJ-2", project.FormatTaskKey(second.Number));
        Assert.Equal(3, project.NextTaskNumber);
    }

    [Fact]
    public void Create_copies_project_and_organization_and_starts_in_todo()
    {
        var project = CreateProject();
        var reporter = Guid.NewGuid();

        var task = TaskItem.Create(project, reporter, TaskType.Bug, " Fix crash ", " ", TaskPriority.High, Now).Value;

        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(project.OrganizationId, task.OrganizationId);
        Assert.Equal(reporter, task.ReporterId);
        Assert.Equal("Fix crash", task.Title);
        Assert.Null(task.Description);
        Assert.Equal(TaskType.Bug, task.Type);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Equal(Now, task.UpdatedAt);
    }

    [Fact]
    public void Create_validates_input_without_consuming_a_task_number()
    {
        var project = CreateProject();

        Assert.Equal(TaskErrors.TitleRequired, TaskItem.Create(project, Guid.NewGuid(), TaskType.Task, " ", null, TaskPriority.Low, Now).Error);
        Assert.Equal(TaskErrors.TypeInvalid, TaskItem.Create(project, Guid.NewGuid(), (TaskType)9, "T", null, TaskPriority.Low, Now).Error);
        Assert.Equal(TaskErrors.PriorityInvalid, TaskItem.Create(project, Guid.NewGuid(), TaskType.Task, "T", null, (TaskPriority)9, Now).Error);
        Assert.Equal(TaskErrors.UserRequired, TaskItem.Create(project, Guid.Empty, TaskType.Task, "T", null, TaskPriority.Low, Now).Error);
        Assert.Equal(1, project.NextTaskNumber);
    }

    [Fact]
    public void Create_is_rejected_in_an_archived_project()
    {
        var project = CreateProject();
        project.Archive();

        Assert.Equal(
            ProjectErrors.Archived,
            TaskItem.Create(project, Guid.NewGuid(), TaskType.Task, "T", null, TaskPriority.Low, Now).Error);
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Review)]
    [InlineData(TaskItemStatus.Review, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Review, TaskItemStatus.InProgress)]
    public void ChangeStatus_allows_the_workflow_transitions(TaskItemStatus from, TaskItemStatus to)
    {
        var task = CreateTaskIn(CreateProject(), from);

        var result = task.ChangeStatus(to, Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, task.Status);
        Assert.Equal(Later, task.UpdatedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.Review)]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Review, TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.Review)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.InProgress)]
    public void ChangeStatus_rejects_other_transitions(TaskItemStatus from, TaskItemStatus to)
    {
        var task = CreateTaskIn(CreateProject(), from);

        var result = task.ChangeStatus(to, Later);

        Assert.Equal("Task.InvalidTransition", result.Error.Code);
        Assert.Equal(from, task.Status);
    }

    [Fact]
    public void ChangeStatus_rejects_undefined_statuses()
    {
        Assert.Equal(TaskErrors.StatusInvalid, CreateTask(CreateProject()).ChangeStatus((TaskItemStatus)9, Later).Error);
    }

    [Fact]
    public void Done_task_cannot_be_edited()
    {
        var project = CreateProject();
        var task = CreateTaskIn(project, TaskItemStatus.Done);
        var label = Label.Create(project, "api", "#000000", Now).Value;

        Assert.Equal(TaskErrors.Closed, task.UpdateDetails(TaskType.Task, "New", null, TaskPriority.Low, Later).Error);
        Assert.Equal(TaskErrors.Closed, task.Estimate(3, Later).Error);
        Assert.Equal(TaskErrors.Closed, task.Assign(Guid.NewGuid(), Later).Error);
        Assert.Equal(TaskErrors.Closed, task.MoveToSprint(null, Later).Error);
        Assert.Equal(TaskErrors.Closed, task.SetEpic(null, Later).Error);
        Assert.Equal(TaskErrors.Closed, task.AddLabel(label, Later).Error);
    }

    [Fact]
    public void Reopen_moves_a_done_task_back_to_in_progress()
    {
        var task = CreateTaskIn(CreateProject(), TaskItemStatus.Done);

        Assert.True(task.Reopen(Later).IsSuccess);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.True(task.Estimate(5, Later).IsSuccess);
    }

    [Fact]
    public void Reopen_requires_a_done_task()
    {
        Assert.Equal(TaskErrors.NotDone, CreateTaskIn(CreateProject(), TaskItemStatus.Review).Reopen(Later).Error);
    }

    [Fact]
    public void UpdateDetails_changes_the_task()
    {
        var task = CreateTask(CreateProject());

        var result = task.UpdateDetails(TaskType.Bug, " Fix login ", "Steps", TaskPriority.Critical, Later);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fix login", task.Title);
        Assert.Equal("Steps", task.Description);
        Assert.Equal(TaskType.Bug, task.Type);
        Assert.Equal(TaskPriority.Critical, task.Priority);
        Assert.Equal(Later, task.UpdatedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(TaskItem.MaxStoryPoints + 1)]
    public void Estimate_rejects_out_of_range_points(int points)
    {
        Assert.Equal(TaskErrors.StoryPointsOutOfRange, CreateTask(CreateProject()).Estimate(points, Later).Error);
    }

    [Fact]
    public void Estimate_and_assign_accept_null_to_clear()
    {
        var task = CreateTask(CreateProject());
        var assignee = Guid.NewGuid();

        task.Estimate(8, Later);
        task.Assign(assignee, Later);
        Assert.Equal(8, task.StoryPoints);
        Assert.Equal(assignee, task.AssigneeId);

        task.Estimate(null, Later);
        task.Assign(null, Later);
        Assert.Null(task.StoryPoints);
        Assert.Null(task.AssigneeId);
        Assert.Equal(TaskErrors.UserRequired, task.Assign(Guid.Empty, Later).Error);
    }

    [Fact]
    public void MoveToSprint_checks_project_and_sprint_status()
    {
        var project = CreateProject();
        var task = CreateTask(project);
        var sprint = Sprint.Create(project, "S1", null, null, null, Now).Value;
        var otherSprint = Sprint.Create(CreateProject("OTH"), "S1", null, null, null, Now).Value;
        var completed = Sprint.Create(project, "S0", null, null, null, Now).Value;
        completed.Start([], Today);
        completed.Complete(Today);

        Assert.True(task.MoveToSprint(sprint, Later).IsSuccess);
        Assert.Equal(sprint.Id, task.SprintId);
        Assert.Equal(TaskErrors.SprintFromAnotherProject, task.MoveToSprint(otherSprint, Later).Error);
        Assert.Equal(TaskErrors.SprintCompleted, task.MoveToSprint(completed, Later).Error);
        Assert.True(task.MoveToSprint(null, Later).IsSuccess);
        Assert.Null(task.SprintId);
    }

    [Fact]
    public void SetEpic_checks_project_and_epic_status()
    {
        var project = CreateProject();
        var task = CreateTask(project);
        var epic = Epic.Create(project, "Auth", null, Now).Value;
        var otherEpic = Epic.Create(CreateProject("OTH"), "Auth", null, Now).Value;
        var closed = Epic.Create(project, "Old", null, Now).Value;
        closed.Close();

        Assert.True(task.SetEpic(epic, Later).IsSuccess);
        Assert.Equal(epic.Id, task.EpicId);
        Assert.Equal(TaskErrors.EpicFromAnotherProject, task.SetEpic(otherEpic, Later).Error);
        Assert.Equal(TaskErrors.EpicClosed, task.SetEpic(closed, Later).Error);
        Assert.True(task.SetEpic(null, Later).IsSuccess);
        Assert.Null(task.EpicId);
    }

    [Fact]
    public void Labels_can_be_added_once_and_removed()
    {
        var project = CreateProject();
        var task = CreateTask(project);
        var label = Label.Create(project, "api", "#000000", Now).Value;
        var otherLabel = Label.Create(CreateProject("OTH"), "api", "#000000", Now).Value;

        Assert.True(task.AddLabel(label, Later).IsSuccess);
        Assert.True(task.AddLabel(label, Later).IsSuccess);
        var taskLabel = Assert.Single(task.Labels);
        Assert.Equal(label.Id, taskLabel.LabelId);
        Assert.Equal(TaskErrors.LabelFromAnotherProject, task.AddLabel(otherLabel, Later).Error);

        Assert.True(task.RemoveLabel(label.Id, Later).IsSuccess);
        Assert.Empty(task.Labels);
    }

    [Fact]
    public void Deleted_task_rejects_every_change()
    {
        var task = CreateTask(CreateProject());

        Assert.True(task.Delete(Later).IsSuccess);
        Assert.True(task.IsDeleted);
        Assert.Equal(TaskErrors.Deleted, task.Delete(Later).Error);
        Assert.Equal(TaskErrors.Deleted, task.ChangeStatus(TaskItemStatus.InProgress, Later).Error);
        Assert.Equal(TaskErrors.Deleted, task.Estimate(1, Later).Error);
        Assert.Equal(TaskErrors.Deleted, task.Reopen(Later).Error);
    }
}
