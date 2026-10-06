using System.Net;
using System.Text;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Epics;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public class TaskEndpointsTests(ProjectFlowApiFactory api)
{
    // ---------- Create ----------

    [Fact]
    public async Task Developers_create_tasks_with_sequential_keys_and_are_the_reporter()
    {
        var world = await WorldAsync();

        var first = await CreateTaskAsync(world, world.Developer, "First");
        var second = await CreateTaskAsync(world, world.Developer, "Second");

        Assert.Equal("TSK-1", first.Key);
        Assert.Equal("TSK-2", second.Key);
        Assert.Equal(world.Developer.Id, first.ReporterId);
        Assert.Equal(TaskItemStatus.ToDo, first.Status);
        var read = await (await world.Viewer.Client.GetAsync($"{world.Tasks}/{first.Id}")).ReadAsync<TaskResponse>();
        Assert.Equal("First", read.Title);
    }

    [Fact]
    public async Task Tasks_created_at_the_same_time_all_succeed_with_distinct_numbers()
    {
        var world = await WorldAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(i =>
            world.Org.Admin.Client.PostJsonAsync(world.Tasks, NewTask($"Concurrent {i}"))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var numbers = new List<int>();
        foreach (var response in responses)
        {
            numbers.Add((await response.ReadAsync<TaskResponse>()).Number);
        }

        Assert.Equal(Enumerable.Range(1, 10), numbers.Order());
    }

    [Fact]
    public async Task Create_can_place_and_assign_the_task_in_one_request()
    {
        var world = await WorldAsync();
        var sprint = await CreateSprintAsync(world);
        var epic = await CreateEpicAsync(world);

        var response = await world.Org.Admin.Client.PostJsonAsync(
            world.Tasks,
            new CreateTaskRequest(TaskType.Bug, "Crash on login", "Steps", TaskPriority.Critical, 3, world.Developer.Id, sprint, epic));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.ReadAsync<TaskResponse>();
        Assert.Equal((world.Developer.Id, sprint, epic, 3), (task.AssigneeId!.Value, task.SprintId!.Value, task.EpicId!.Value, task.StoryPoints!.Value));
        Assert.Equal(TaskPriority.Critical, task.Priority);
    }

    [Fact]
    public async Task Viewers_cannot_create_tasks()
    {
        var world = await WorldAsync();

        var response = await world.Viewer.Client.PostJsonAsync(world.Tasks, NewTask("Nope"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "type": "Task", "title": "", "priority": "Low" }""")]
    [InlineData("""{ "type": "Task", "title": "Too many points", "priority": "Low", "storyPoints": 101 }""")]
    [InlineData("""{ "type": "Epic", "title": "Unknown type", "priority": "Low" }""")]
    public async Task Invalid_tasks_are_rejected_with_400(string body)
    {
        var world = await WorldAsync();

        var response = await world.Org.Admin.Client.PostAsync(world.Tasks, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Who can edit what ----------

    [Fact]
    public async Task Developers_edit_only_tasks_they_reported_or_are_assigned_to()
    {
        var world = await WorldAsync();
        var other = await world.Org.AddProjectUserAsync(world.ProjectId, "other-dev", ProjectRole.Developer);
        var reportedByMe = await CreateTaskAsync(world, world.Developer, "Mine");
        var assignedToMe = await CreateTaskAsync(world, world.Org.Admin, "Assigned to me", assignee: world.Developer.Id);
        var someoneElses = await CreateTaskAsync(world, other, "Not mine");

        var editMine = await Update(world, world.Developer, reportedByMe.Id, "Mine, edited");
        var editAssigned = await Update(world, world.Developer, assignedToMe.Id, "Assigned, edited");
        var editOthers = await Update(world, world.Developer, someoneElses.Id, "Hacked");
        var moveOthers = await world.Developer.Client.PutJsonAsync($"{world.Tasks}/{someoneElses.Id}/sprint", new MoveTaskToSprintRequest(null));

        Assert.Equal(HttpStatusCode.NoContent, editMine.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, editAssigned.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editOthers.StatusCode);
        Assert.Equal("Task.NotYourTask", await editOthers.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, moveOthers.StatusCode);
        Assert.Equal("Not mine", (await (await other.Client.GetAsync($"{world.Tasks}/{someoneElses.Id}")).ReadAsync<TaskResponse>()).Title);
    }

    [Fact]
    public async Task Project_managers_edit_any_task_and_viewers_none()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer, "Developer's task");

        Assert.Equal(HttpStatusCode.NoContent, (await Update(world, world.Org.Admin, task.Id, "Edited by PM")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Update(world, world.Viewer, task.Id, "Edited by viewer")).StatusCode);
    }

    [Fact]
    public async Task Done_tasks_are_read_only()
    {
        var world = await WorldAsync();
        var done = await api.AddTaskAsync(world.Org.Id, world.ProjectId, status: TaskItemStatus.Done);

        var response = await Update(world, world.Org.Admin, done, "Too late");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Task.Closed", await response.ReadErrorCodeAsync());
    }

    // ---------- Assign, sprint, epic ----------

    [Fact]
    public async Task Tasks_can_only_be_assigned_to_people_who_work_in_the_project()
    {
        var world = await WorldAsync();
        var outsider = await world.Org.AddMemberAsync("no-project-role");
        var task = await CreateTaskAsync(world, world.Org.Admin, "Assign me");
        var url = $"{world.Tasks}/{task.Id}/assignee";

        var toDeveloper = await world.Org.Admin.Client.PutJsonAsync(url, new AssignTaskRequest(world.Developer.Id));
        var toViewer = await world.Org.Admin.Client.PutJsonAsync(url, new AssignTaskRequest(world.Viewer.Id));
        var toOutsider = await world.Org.Admin.Client.PutJsonAsync(url, new AssignTaskRequest(outsider.Id));
        var assigned = await (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{task.Id}")).ReadAsync<TaskResponse>();
        var unassign = await world.Org.Admin.Client.PutJsonAsync(url, new AssignTaskRequest(null));

        Assert.Equal(HttpStatusCode.NoContent, toDeveloper.StatusCode);
        Assert.Equal(world.Developer.Id, assigned.AssigneeId);
        Assert.Equal("Task.AssigneeNotAllowed", await toViewer.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, toOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unassign.StatusCode);
    }

    [Fact]
    public async Task Tasks_move_between_backlog_and_sprints_of_their_project_only()
    {
        var world = await WorldAsync();
        var sprint = await CreateSprintAsync(world);
        var otherProject = (await world.Org.CreateProjectAsync("OTH")).Id;
        var otherSprint = await CreateSprintAsync(world, otherProject);
        var task = await CreateTaskAsync(world, world.Org.Admin, "Move me");
        var url = $"{world.Tasks}/{task.Id}/sprint";

        Assert.Equal(HttpStatusCode.NoContent, (await world.Org.Admin.Client.PutJsonAsync(url, new MoveTaskToSprintRequest(sprint))).StatusCode);
        Assert.Equal(sprint, (await (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{task.Id}")).ReadAsync<TaskResponse>()).SprintId);
        var foreign = await world.Org.Admin.Client.PutJsonAsync(url, new MoveTaskToSprintRequest(otherSprint));
        Assert.Equal(HttpStatusCode.NoContent, (await world.Org.Admin.Client.PutJsonAsync(url, new MoveTaskToSprintRequest(null))).StatusCode);

        Assert.Equal("Task.SprintNotInProject", await foreign.ReadErrorCodeAsync());
        Assert.Null((await (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{task.Id}")).ReadAsync<TaskResponse>()).SprintId);
    }

    [Fact]
    public async Task Tasks_cannot_join_completed_sprints_or_closed_epics()
    {
        var world = await WorldAsync();
        var sprint = await CreateSprintAsync(world);
        await world.Org.Admin.Client.PostAsync($"{world.Project}/sprints/{sprint}/start", null);
        await world.Org.Admin.Client.PostAsync($"{world.Project}/sprints/{sprint}/complete", null);
        var epic = await CreateEpicAsync(world);
        await world.Org.Admin.Client.PostAsync($"{world.Project}/epics/{epic}/close", null);
        var task = await CreateTaskAsync(world, world.Org.Admin, "Late");

        var toSprint = await world.Org.Admin.Client.PutJsonAsync($"{world.Tasks}/{task.Id}/sprint", new MoveTaskToSprintRequest(sprint));
        var toEpic = await world.Org.Admin.Client.PutJsonAsync($"{world.Tasks}/{task.Id}/epic", new SetTaskEpicRequest(epic));
        var toMissingEpic = await world.Org.Admin.Client.PutJsonAsync($"{world.Tasks}/{task.Id}/epic", new SetTaskEpicRequest(Guid.NewGuid()));

        Assert.Equal("Task.SprintCompleted", await toSprint.ReadErrorCodeAsync());
        Assert.Equal("Task.EpicClosed", await toEpic.ReadErrorCodeAsync());
        Assert.Equal("Task.EpicNotInProject", await toMissingEpic.ReadErrorCodeAsync());
    }

    // ---------- Delete, archive, isolation ----------

    [Fact]
    public async Task Only_project_managers_delete_tasks_and_deleted_tasks_disappear()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer, "Delete me");

        var byDeveloper = await world.Developer.Client.DeleteAsync($"{world.Tasks}/{task.Id}");
        var byManager = await world.Org.Admin.Client.DeleteAsync($"{world.Tasks}/{task.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, byDeveloper.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{task.Id}")).StatusCode);
        Assert.DoesNotContain(
            (await (await world.Org.Admin.Client.GetAsync(world.Tasks)).ReadAsync<PagedResponse<TaskResponse>>()).Items,
            listed => listed.Id == task.Id);
    }

    [Fact]
    public async Task Archived_projects_accept_no_task_changes()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Org.Admin, "Before archive");
        await world.Org.Admin.Client.PostAsync($"{world.Project}/archive", null);

        var create = await world.Org.Admin.Client.PostJsonAsync(world.Tasks, NewTask("After archive"));
        var update = await Update(world, world.Org.Admin, task.Id, "After archive");

        Assert.Equal("Project.Archived", await create.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, update.StatusCode);
        Assert.Equal("Project.Archived", await update.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task A_task_cannot_be_reached_through_another_project()
    {
        var world = await WorldAsync();
        var otherProject = (await world.Org.CreateProjectAsync("OTH")).Id;
        var task = await CreateTaskAsync(world, world.Org.Admin, "Mine");

        var read = await world.Org.Admin.Client.GetAsync($"{world.Org.Project(otherProject)}/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("Task.NotFound", await read.ReadErrorCodeAsync());
    }

    // ---------- Helpers ----------

    /// <summary>An organization whose admin is the project's PM, plus a developer and a viewer.</summary>
    private sealed record World(TestOrganization Org, Guid ProjectId, ApiUser Developer, ApiUser Viewer)
    {
        public string Project => Org.Project(ProjectId);

        public string Tasks => $"{Project}/tasks";
    }

    private async Task<World> WorldAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = (await org.CreateProjectAsync("TSK")).Id;
        var developer = await org.AddProjectUserAsync(project, "developer", ProjectRole.Developer);
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);
        return new World(org, project, developer, viewer);
    }

    private static CreateTaskRequest NewTask(string title, Guid? assignee = null) =>
        new(TaskType.Task, title, null, TaskPriority.Medium, null, assignee, null, null);

    private static async Task<TaskResponse> CreateTaskAsync(World world, ApiUser user, string title, Guid? assignee = null)
    {
        var response = await user.Client.PostJsonAsync(world.Tasks, NewTask(title, assignee));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<TaskResponse>();
    }

    private static Task<HttpResponseMessage> Update(World world, ApiUser user, Guid taskId, string title) =>
        user.Client.PutJsonAsync($"{world.Tasks}/{taskId}", new UpdateTaskRequest(TaskType.Task, title, null, TaskPriority.High, 5));

    private static async Task<Guid> CreateSprintAsync(World world, Guid? projectId = null)
    {
        var url = $"{world.Org.Project(projectId ?? world.ProjectId)}/sprints";
        var response = await world.Org.Admin.Client.PostJsonAsync(url, new SprintRequest($"Sprint {TestData.Unique()}", null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<SprintResponse>()).Id;
    }

    private static async Task<Guid> CreateEpicAsync(World world)
    {
        var response = await world.Org.Admin.Client.PostJsonAsync($"{world.Project}/epics", new EpicRequest($"Epic {TestData.Unique()}", null));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<EpicResponse>()).Id;
    }
}
