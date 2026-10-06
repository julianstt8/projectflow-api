using ProjectFlow.Domain.Activity;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Activity;

public class ActivityEventsTests
{
    private static List<ProjectActivityEvent> Events(Entity entity) =>
        entity.PullDomainEvents().OfType<ProjectActivityEvent>().ToList();

    [Fact]
    public void Creating_a_task_raises_an_event_with_the_title()
    {
        var project = CreateProject();
        Events(project);

        var task = CreateTask(project);

        var created = Assert.IsType<TaskCreated>(Assert.Single(Events(task)));
        Assert.Equal((project.OrganizationId, project.Id, "Task", task.Id), (created.OrganizationId, created.ProjectId, created.EntityType, created.EntityId));
        Assert.Equal(("Created", "Implement login"), (created.Action, created.NewValue));
    }

    [Fact]
    public void Status_changes_record_old_and_new_values()
    {
        var task = CreateTask(CreateProject());
        Events(task);

        task.ChangeStatus(TaskItemStatus.InProgress, Now);

        var changed = Assert.Single(Events(task));
        Assert.Equal(("StatusChanged", "ToDo", "InProgress"), (changed.Action, changed.OldValue, changed.NewValue));
    }

    [Fact]
    public void Rejected_changes_raise_nothing()
    {
        var task = CreateTask(CreateProject());
        Events(task);

        task.ChangeStatus(TaskItemStatus.Done, Now);
        task.Estimate(500, Now);

        Assert.Empty(Events(task));
    }

    [Fact]
    public void Setting_the_same_value_again_raises_nothing()
    {
        var task = CreateTask(CreateProject());
        var assignee = Guid.NewGuid();
        task.Assign(assignee, Now);
        task.Estimate(3, Now);
        Events(task);

        task.Assign(assignee, Now);
        task.Estimate(3, Now);
        task.UpdateDetails(task.Type, task.Title, "only the description changed", task.Priority, Now);

        Assert.Empty(Events(task));
    }

    [Fact]
    public void Assignment_sprint_and_epic_changes_record_the_ids()
    {
        var project = CreateProject();
        var sprint = Sprint.Create(project, "S1", null, null, null, Now).Value;
        var task = CreateTask(project);
        var assignee = Guid.NewGuid();
        Events(task);

        task.Assign(assignee, Now);
        task.MoveToSprint(sprint, Now);
        task.MoveToSprint(null, Now);

        Assert.Equal(
            [("AssigneeChanged", null, assignee.ToString()), ("SprintChanged", null, sprint.Id.ToString()), ("SprintChanged", sprint.Id.ToString(), null)],
            Events(task).Select(e => (e.Action, e.OldValue, e.NewValue)));
    }

    [Fact]
    public void Pulling_events_forgets_them()
    {
        var task = CreateTask(CreateProject());

        Assert.NotEmpty(task.PullDomainEvents());
        Assert.Empty(task.PullDomainEvents());
    }

    [Fact]
    public void Project_and_sprint_lifecycle_and_membership_are_recorded()
    {
        var project = CreateProject();
        var sprint = Sprint.Create(project, "Sprint 1", null, null, null, Now).Value;
        var developer = Guid.NewGuid();
        Events(project);

        project.AddMember(developer, ProjectRole.Developer);
        project.ChangeMemberRole(developer, ProjectRole.Viewer);
        project.RemoveMember(developer);
        project.Archive();
        sprint.Start([sprint], Today);

        Assert.Equal(
            ["MemberAdded", "MemberRoleChanged", "MemberRemoved", "Archived"],
            Events(project).Select(e => e.Action));
        Assert.Equal(["Created", "Started"], Events(sprint).Select(e => e.Action));
    }

    [Fact]
    public void Activity_log_entries_copy_the_event_and_cap_long_values()
    {
        var change = new TaskTitleChanged(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "old", new string('x', 600));
        var actor = Guid.NewGuid();

        var log = ActivityLog.Record(change, actor, Now);

        Assert.Equal((change.OrganizationId, change.ProjectId, change.TaskId, actor), (log.OrganizationId, log.ProjectId, log.EntityId, log.ActorId));
        Assert.Equal(("Task", "TitleChanged", "old", Now), (log.EntityType, log.Action, log.OldValue, log.CreatedAt));
        Assert.Equal(ActivityLog.ValueMaxLength, log.NewValue!.Length);
    }
}
