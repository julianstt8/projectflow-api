using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Domain.Tasks.TaskItemStatus;

namespace ProjectFlow.Api.IntegrationTests.Tasks;

/// <summary>RF-07 (status workflow) and RF-08 (only project managers reopen done tasks) through the API.</summary>
[Collection(ApiCollection.Name)]
public class TaskStatusWorkflowTests(ProjectFlowApiFactory api)
{
    /// <summary>Every from → to pair, written out by hand from the PRD.</summary>
    public static TheoryData<TaskItemStatus, TaskItemStatus, HttpStatusCode> Transitions()
    {
        (TaskItemStatus From, TaskItemStatus To)[] allowed =
            [(ToDo, InProgress), (InProgress, Review), (Review, Done), (Review, InProgress)];

        var data = new TheoryData<TaskItemStatus, TaskItemStatus, HttpStatusCode>();
        foreach (var from in Enum.GetValues<TaskItemStatus>())
        {
            foreach (var to in Enum.GetValues<TaskItemStatus>())
            {
                data.Add(from, to, allowed.Contains((from, to)) ? HttpStatusCode.NoContent : HttpStatusCode.UnprocessableEntity);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public async Task Only_the_workflow_transitions_of_the_PRD_are_allowed(TaskItemStatus from, TaskItemStatus to, HttpStatusCode expected)
    {
        var world = await WorldAsync();
        var task = await api.AddTaskAsync(world.Org.Id, world.ProjectId, status: from);

        var response = await ChangeStatus(world.Org.Admin, world, task, to);

        Assert.Equal(expected, response.StatusCode);
        var current = await GetAsync(world, task);
        if (expected == HttpStatusCode.NoContent)
        {
            Assert.Equal(to, current.Status);
        }
        else
        {
            Assert.Equal("Task.InvalidTransition", await response.ReadErrorCodeAsync());
            Assert.Equal(from, current.Status);
        }
    }

    [Fact]
    public async Task A_developer_takes_their_own_task_from_todo_to_done()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);

        foreach (var status in new[] { InProgress, Review, Done })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await ChangeStatus(world.Developer, world, task, status)).StatusCode);
        }

        Assert.Equal(Done, (await GetAsync(world, task)).Status);
    }

    [Fact]
    public async Task Developers_cannot_move_tasks_of_others_and_viewers_cannot_move_any()
    {
        var world = await WorldAsync();
        var managersTask = await CreateTaskAsync(world, world.Org.Admin);

        var byDeveloper = await ChangeStatus(world.Developer, world, managersTask, InProgress);
        var byViewer = await ChangeStatus(world.Viewer, world, managersTask, InProgress);

        Assert.Equal(HttpStatusCode.Forbidden, byDeveloper.StatusCode);
        Assert.Equal("Task.NotYourTask", await byDeveloper.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, byViewer.StatusCode);
        Assert.Equal(ToDo, (await GetAsync(world, managersTask)).Status);
    }

    [Fact]
    public async Task Only_project_managers_reopen_done_tasks_which_become_editable_again()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);
        foreach (var status in new[] { InProgress, Review, Done })
        {
            await ChangeStatus(world.Developer, world, task, status);
        }

        var editWhileDone = await UpdateAsync(world.Developer, world, task, "Too late");
        var reopenByDeveloper = await world.Developer.Client.PostAsync($"{world.Tasks}/{task}/reopen", null);
        var reopenByManager = await world.Org.Admin.Client.PostAsync($"{world.Tasks}/{task}/reopen", null);
        var editAfterReopen = await UpdateAsync(world.Developer, world, task, "Fixed after reopening");

        Assert.Equal("Task.Closed", await editWhileDone.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, reopenByDeveloper.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reopenByManager.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, editAfterReopen.StatusCode);
        var reopened = await GetAsync(world, task);
        Assert.Equal(InProgress, reopened.Status);
        Assert.Equal("Fixed after reopening", reopened.Title);
    }

    [Fact]
    public async Task Only_done_tasks_can_be_reopened()
    {
        var world = await WorldAsync();
        var task = await api.AddTaskAsync(world.Org.Id, world.ProjectId, status: Review);

        var response = await world.Org.Admin.Client.PostAsync($"{world.Tasks}/{task}/reopen", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Task.NotDone", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Unknown_status_is_rejected_with_400()
    {
        var world = await WorldAsync();
        var task = await api.AddTaskAsync(world.Org.Id, world.ProjectId);

        var response = await world.Org.Admin.Client.PostAsync(
            $"{world.Tasks}/{task}/status",
            new StringContent("""{ "status": "Blocked" }""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Helpers ----------

    private sealed record World(TestOrganization Org, Guid ProjectId, ApiUser Developer, ApiUser Viewer)
    {
        public string Tasks => $"{Org.Project(ProjectId)}/tasks";
    }

    private async Task<World> WorldAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = (await org.CreateProjectAsync("FLW")).Id;
        var developer = await org.AddProjectUserAsync(project, "developer", ProjectRole.Developer);
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);
        return new World(org, project, developer, viewer);
    }

    private static async Task<Guid> CreateTaskAsync(World world, ApiUser reporter)
    {
        var response = await reporter.Client.PostJsonAsync(
            world.Tasks,
            new CreateTaskRequest(TaskType.Task, "Workflow task", null, TaskPriority.Medium, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<TaskResponse>()).Id;
    }

    private static Task<HttpResponseMessage> ChangeStatus(ApiUser user, World world, Guid taskId, TaskItemStatus status) =>
        user.Client.PostJsonAsync($"{world.Tasks}/{taskId}/status", new ChangeTaskStatusRequest(status));

    private static Task<HttpResponseMessage> UpdateAsync(ApiUser user, World world, Guid taskId, string title) =>
        user.Client.PutJsonAsync($"{world.Tasks}/{taskId}", new UpdateTaskRequest(TaskType.Task, title, null, TaskPriority.Medium, null));

    private static async Task<TaskResponse> GetAsync(World world, Guid taskId) =>
        await (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{taskId}")).ReadAsync<TaskResponse>();
}
