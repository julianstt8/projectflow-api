using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Epics;
using ProjectFlow.Application.Labels;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Tasks;

/// <summary>RF-11: task search with filters, text search (case and accent insensitive), sorting and paging.</summary>
[Collection(ApiCollection.Name)]
public sealed class TaskSearchTests(ProjectFlowApiFactory api) : IAsyncLifetime
{
    private static readonly SemaphoreSlim SetupLock = new(1, 1);
    private static Scenario? _scenario;

    private Scenario World => _scenario!;

    public async Task InitializeAsync()
    {
        await SetupLock.WaitAsync();
        try
        {
            _scenario ??= await Scenario.CreateAsync(api);
        }
        finally
        {
            SetupLock.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Without_filters_every_visible_task_is_returned_by_number()
    {
        var result = await SearchAsync("");

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Numbers(result));
        Assert.Equal(5, result.TotalCount);
    }

    [Theory]
    [InlineData("?status=ToDo&status=Review", new[] { 1, 3, 5 })]
    [InlineData("?status=Done", new[] { 4 })]
    [InlineData("?unassigned=true", new[] { 3, 4 })]
    [InlineData("?backlog=true", new[] { 3, 4 })]
    public async Task Filters_by_status_assignee_and_sprint(string query, int[] expected)
    {
        Assert.Equal(expected, Numbers(await SearchAsync(query)));
    }

    [Fact]
    public async Task Filters_by_assignee_sprint_epic_and_label_ids()
    {
        Assert.Equal(new[] { 1, 2 }, Numbers(await SearchAsync($"?assigneeId={World.Developer.Id}")));
        Assert.Equal(new[] { 1, 2, 5 }, Numbers(await SearchAsync($"?sprintId={World.SprintId}")));
        Assert.Equal(new[] { 1, 5 }, Numbers(await SearchAsync($"?epicId={World.EpicId}")));
        Assert.Equal(new[] { 1 }, Numbers(await SearchAsync($"?labelId={World.LabelId}")));
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        Assert.Equal(new[] { 1, 5 }, Numbers(await SearchAsync($"?status=ToDo&sprintId={World.SprintId}")));
        Assert.Equal(new[] { 1 }, Numbers(await SearchAsync($"?status=ToDo&sprintId={World.SprintId}&assigneeId={World.Developer.Id}")));
    }

    [Theory]
    [InlineData("validacion", new[] { 1 })]
    [InlineData("VALIDACIÓN", new[] { 1 })]
    [InlineData("diseno", new[] { 3 })]
    [InlineData("login", new[] { 1, 2 })]
    [InlineData("Ñandú", new int[0])]
    public async Task Text_search_ignores_case_and_accents_and_skips_deleted_tasks(string text, int[] expected)
    {
        // Task 6 "Validación duplicada" is deleted and never appears.
        Assert.Equal(expected, Numbers(await SearchAsync($"?q={Uri.EscapeDataString(text)}")));
    }

    [Theory]
    [InlineData("SRC-2", 2)]
    [InlineData("src-3", 3)]
    [InlineData("#4", 4)]
    [InlineData("5", 5)]
    public async Task Tasks_can_be_found_by_key_or_number(string text, int expected)
    {
        Assert.Equal(new[] { expected }, Numbers(await SearchAsync($"?q={Uri.EscapeDataString(text)}")));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("100%")]
    [InlineData("worker_pool")]
    public async Task Like_wildcards_in_the_text_are_searched_literally(string text)
    {
        Assert.Equal(new[] { 4 }, Numbers(await SearchAsync($"?q={Uri.EscapeDataString(text)}")));
    }

    [Fact]
    public async Task Results_can_be_sorted()
    {
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, Numbers(await SearchAsync("?sort=-number")));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Numbers(await SearchAsync("?sort=number")));
        Assert.Equal(5, (await SearchAsync("?sort=-updatedAt")).Items.Count);
    }

    [Fact]
    public async Task Results_are_paged_with_the_total_count()
    {
        var first = await SearchAsync("?pageSize=2&page=1");
        var last = await SearchAsync("?pageSize=2&page=3");
        var beyond = await SearchAsync("?pageSize=2&page=9");

        Assert.Equal(new[] { 1, 2 }, Numbers(first));
        Assert.Equal((5, 3, true), (first.TotalCount, first.TotalPages, first.HasNextPage));
        Assert.Equal(new[] { 5 }, Numbers(last));
        Assert.False(last.HasNextPage);
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    [InlineData("?sort=priority")]
    [InlineData("?status=Blocked")]
    [InlineData("?unassigned=true&assigneeId=6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("?backlog=true&sprintId=6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    public async Task Invalid_searches_return_400(string query)
    {
        var response = await World.Org.Admin.Client.GetAsync($"{World.Tasks}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Viewers_search_but_outsiders_cannot()
    {
        var outsider = await api.CreateUserAsync("outsider");

        Assert.Equal(HttpStatusCode.OK, (await World.Viewer.Client.GetAsync(World.Tasks)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync(World.Tasks)).StatusCode);
    }

    private async Task<PagedResponse<TaskResponse>> SearchAsync(string query)
    {
        var response = await World.Org.Admin.Client.GetAsync($"{World.Tasks}{query}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<PagedResponse<TaskResponse>>();
    }

    private static int[] Numbers(PagedResponse<TaskResponse> result) => result.Items.Select(task => task.Number).ToArray();

    /// <summary>Five visible tasks with varied attributes plus a deleted one, created once for the class.</summary>
    private sealed record Scenario(TestOrganization Org, string Tasks, ApiUser Developer, ApiUser Viewer, Guid SprintId, Guid EpicId, Guid LabelId)
    {
        public static async Task<Scenario> CreateAsync(ProjectFlowApiFactory api)
        {
            var org = await api.CreateOrganizationAsync();
            var project = (await org.CreateProjectAsync("SRC")).Id;
            var url = org.Project(project);
            var developer = await org.AddProjectUserAsync(project, "developer", ProjectRole.Developer);
            var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);
            var admin = org.Admin.Client;

            var sprint = (await (await admin.PostJsonAsync($"{url}/sprints", new SprintRequest("Sprint 1", null, null, null))).ReadAsync<SprintResponse>()).Id;
            var epic = (await (await admin.PostJsonAsync($"{url}/epics", new EpicRequest("Autenticación", null))).ReadAsync<EpicResponse>()).Id;
            var label = (await (await admin.PostJsonAsync($"{url}/labels", new LabelRequest("backend", "#1D76DB"))).ReadAsync<LabelResponse>()).Id;

            async Task<Guid> Task(string title, Guid? assignee, Guid? sprintId, Guid? epicId, TaskItemStatus status)
            {
                var created = await admin.PostJsonAsync(
                    $"{url}/tasks",
                    new CreateTaskRequest(TaskType.Task, title, null, TaskPriority.Medium, null, assignee, sprintId, epicId));
                var id = (await created.ReadAsync<TaskResponse>()).Id;
                foreach (var next in new[] { TaskItemStatus.InProgress, TaskItemStatus.Review, TaskItemStatus.Done }.TakeWhile(_ => status != TaskItemStatus.ToDo))
                {
                    await admin.PostJsonAsync($"{url}/tasks/{id}/status", new ChangeTaskStatusRequest(next));
                    if (next == status)
                    {
                        break;
                    }
                }

                return id;
            }

            var one = await Task("Validación del formulario de login", developer.Id, sprint, epic, TaskItemStatus.ToDo);
            await admin.PutAsync($"{url}/tasks/{one}/labels/{label}", null);
            await Task("Login with Google", developer.Id, sprint, null, TaskItemStatus.InProgress);
            await Task("Diseño del dashboard", null, null, null, TaskItemStatus.Review);
            await Task("Fix 100% CPU in worker_pool", null, null, null, TaskItemStatus.Done);
            await Task("Export CSV", org.Admin.Id, sprint, epic, TaskItemStatus.ToDo);
            var deleted = await Task("Validación duplicada", null, null, null, TaskItemStatus.ToDo);
            await admin.DeleteAsync($"{url}/tasks/{deleted}");

            return new Scenario(org, $"{url}/tasks", developer, viewer, sprint, epic, label);
        }
    }
}
