using System.Net;
using Microsoft.EntityFrameworkCore;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Sprints;

[Collection(ApiCollection.Name)]
public class SprintEndpointsTests(ProjectFlowApiFactory api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Project_manager_plans_a_sprint_and_everyone_in_the_project_sees_it()
    {
        var (org, project) = await ProjectAsync();
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);

        var response = await org.Admin.Client.PostJsonAsync(Sprints(org, project), new SprintRequest("Sprint 1", "Login", Today, Today.AddDays(13)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<SprintResponse>();
        Assert.Equal(SprintStatus.Planned, created.Status);
        Assert.Equal(0, created.TaskCount);
        Assert.NotNull(response.Headers.Location);

        var list = await (await viewer.Client.GetAsync(Sprints(org, project))).ReadAsync<List<SprintResponse>>();
        Assert.Equal(created.Id, Assert.Single(list).Id);
        Assert.Equal(HttpStatusCode.OK, (await viewer.Client.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Theory]
    [InlineData(ProjectRole.Developer)]
    [InlineData(ProjectRole.Viewer)]
    public async Task Only_admins_and_project_managers_manage_sprints(ProjectRole role)
    {
        var (org, project) = await ProjectAsync();
        var user = await org.AddProjectUserAsync(project, "user", role);
        var sprint = await CreateSprintAsync(org, project);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostJsonAsync(Sprints(org, project), new SprintRequest("S", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"{Sprints(org, project)}/{sprint.Id}/start", null)).StatusCode);
    }

    [Fact]
    public async Task End_date_before_start_date_is_rejected()
    {
        var (org, project) = await ProjectAsync();

        var response = await org.Admin.Client.PostJsonAsync(Sprints(org, project), new SprintRequest("S1", null, Today, Today.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Sprint.EndBeforeStart", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Full_cycle_planned_active_completed_with_one_active_sprint_at_a_time()
    {
        var (org, project) = await ProjectAsync();
        var first = await CreateSprintAsync(org, project);
        var second = await CreateSprintAsync(org, project);

        Assert.Equal(HttpStatusCode.NoContent, (await Start(org, project, first.Id)).StatusCode);
        var secondWhileFirstActive = await Start(org, project, second.Id);
        Assert.Equal(HttpStatusCode.Conflict, secondWhileFirstActive.StatusCode);
        Assert.Equal("Sprint.AnotherSprintActive", await secondWhileFirstActive.ReadErrorCodeAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await Complete(org, project, first.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Start(org, project, second.Id)).StatusCode);

        var completed = await (await org.Admin.Client.GetAsync($"{Sprints(org, project)}/{first.Id}")).ReadAsync<SprintResponse>();
        Assert.Equal(SprintStatus.Completed, completed.Status);
        Assert.Equal(Today, completed.StartDate);
        Assert.Equal(Today, completed.EndDate);
    }

    [Fact]
    public async Task Invalid_transitions_and_changes_to_completed_sprints_are_rejected()
    {
        var (org, project) = await ProjectAsync();
        var sprint = await CreateSprintAsync(org, project);

        var completePlanned = await Complete(org, project, sprint.Id);
        await Start(org, project, sprint.Id);
        await Complete(org, project, sprint.Id);
        var restart = await Start(org, project, sprint.Id);
        var update = await org.Admin.Client.PutJsonAsync($"{Sprints(org, project)}/{sprint.Id}", new SprintRequest("Renamed", null, null, null));

        Assert.Equal("Sprint.NotActive", await completePlanned.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, restart.StatusCode);
        Assert.Equal("Sprint.NotPlanned", await restart.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, update.StatusCode);
        Assert.Equal("Sprint.Completed", await update.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Two_sprints_started_at_the_same_time_leave_only_one_active()
    {
        var (org, project) = await ProjectAsync();
        var sprints = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CreateSprintAsync(org, project)));

        var responses = await Task.WhenAll(sprints.Select(sprint => Start(org, project, sprint.Id)));

        // Each request may pass the domain check; the partial unique index stops the rest with the same error.
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        foreach (var rejected in responses.Where(response => response.StatusCode != HttpStatusCode.NoContent))
        {
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Equal("Sprint.AnotherSprintActive", await rejected.ReadErrorCodeAsync());
        }

        var list = await (await org.Admin.Client.GetAsync(Sprints(org, project))).ReadAsync<List<SprintResponse>>();
        Assert.Single(list, sprint => sprint.Status == SprintStatus.Active);
    }

    [Fact]
    public async Task Completing_a_sprint_moves_unfinished_tasks_back_to_the_backlog()
    {
        var (org, project) = await ProjectAsync();
        var sprint = await CreateSprintAsync(org, project);
        await Start(org, project, sprint.Id);
        var (doneTask, openTask) = await AddTasksToSprintAsync(org.Id, project, sprint.Id);
        var before = await (await org.Admin.Client.GetAsync($"{Sprints(org, project)}/{sprint.Id}")).ReadAsync<SprintResponse>();

        Assert.Equal(HttpStatusCode.NoContent, (await Complete(org, project, sprint.Id)).StatusCode);

        await using var dbContext = api.CreateDbContext(org.Id);
        Assert.Equal(sprint.Id, (await dbContext.Tasks.SingleAsync(task => task.Id == doneTask)).SprintId);
        Assert.Null((await dbContext.Tasks.SingleAsync(task => task.Id == openTask)).SprintId);
        Assert.Equal(2, before.TaskCount);
    }

    [Fact]
    public async Task Archived_projects_accept_no_sprint_changes()
    {
        var (org, project) = await ProjectAsync();
        var sprint = await CreateSprintAsync(org, project);
        await org.Admin.Client.PostAsync($"{org.Project(project)}/archive", null);

        var create = await org.Admin.Client.PostJsonAsync(Sprints(org, project), new SprintRequest("S2", null, null, null));
        var start = await Start(org, project, sprint.Id);

        Assert.Equal("Project.Archived", await create.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, start.StatusCode);
        Assert.Equal("Project.Archived", await start.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task A_sprint_cannot_be_reached_through_another_project()
    {
        var (org, project) = await ProjectAsync();
        var other = (await org.CreateProjectAsync("OTH")).Id;
        var sprint = await CreateSprintAsync(org, project);

        var read = await org.Admin.Client.GetAsync($"{Sprints(org, other)}/{sprint.Id}");
        var start = await Start(org, other, sprint.Id);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("Sprint.NotFound", await read.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
    }

    // ---------- Helpers ----------

    private async Task<(TestOrganization Org, Guid ProjectId)> ProjectAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("SPR");
        return (org, project.Id);
    }

    private static string Sprints(TestOrganization org, Guid projectId) => $"{org.Project(projectId)}/sprints";

    private static async Task<SprintResponse> CreateSprintAsync(TestOrganization org, Guid projectId)
    {
        var response = await org.Admin.Client.PostJsonAsync(Sprints(org, projectId), new SprintRequest($"Sprint {TestData.Unique()}", null, null, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<SprintResponse>();
    }

    private static Task<HttpResponseMessage> Start(TestOrganization org, Guid projectId, Guid sprintId) =>
        org.Admin.Client.PostAsync($"{Sprints(org, projectId)}/{sprintId}/start", null);

    private static Task<HttpResponseMessage> Complete(TestOrganization org, Guid projectId, Guid sprintId) =>
        org.Admin.Client.PostAsync($"{Sprints(org, projectId)}/{sprintId}/complete", null);

    /// <summary>Task endpoints arrive in #16, so tasks are created through the domain here.</summary>
    private async Task<(Guid Done, Guid Open)> AddTasksToSprintAsync(Guid organizationId, Guid projectId, Guid sprintId)
    {
        await using var dbContext = api.CreateDbContext(organizationId);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == projectId);
        var sprint = await dbContext.Sprints.SingleAsync(s => s.Id == sprintId);
        var reporter = project.Members.Count > 0 ? project.Members.First().UserId : (await dbContext.Users.FirstAsync()).Id;
        var now = DateTimeOffset.UtcNow;

        var done = TaskItem.Create(project, reporter, TaskType.Task, "Finished", null, TaskPriority.Low, now).Value;
        var open = TaskItem.Create(project, reporter, TaskType.Task, "Unfinished", null, TaskPriority.Low, now).Value;
        done.MoveToSprint(sprint, now);
        open.MoveToSprint(sprint, now);
        foreach (var status in new[] { TaskItemStatus.InProgress, TaskItemStatus.Review, TaskItemStatus.Done })
        {
            done.ChangeStatus(status, now);
        }

        open.ChangeStatus(TaskItemStatus.InProgress, now);
        dbContext.Tasks.AddRange(done, open);
        await dbContext.SaveChangesAsync();
        return (done.Id, open.Id);
    }
}
