using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Activity;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Activity;

/// <summary>RF-10: who changed what and when, recorded with the change and never modified afterwards.</summary>
[Collection(ApiCollection.Name)]
public class ActivityLogTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Changes_are_logged_with_actor_action_and_values_newest_first()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer, "Login form");
        await world.Developer.Client.PostJsonAsync($"{world.Tasks}/{task}/status", new ChangeTaskStatusRequest(TaskItemStatus.InProgress));
        await world.Org.Admin.Client.PutJsonAsync($"{world.Tasks}/{task}/assignee", new AssignTaskRequest(world.Developer.Id));

        var log = await ReadAsync(world, world.Org.Admin);

        var entries = log.Items.Where(entry => entry.EntityId == task).ToList();
        Assert.Equal(["AssigneeChanged", "StatusChanged", "Created"], entries.Select(e => e.Action));
        Assert.Equal(("ToDo", "InProgress"), (entries[1].OldValue, entries[1].NewValue));
        Assert.Equal("Login form", entries[2].NewValue);
        Assert.Equal(["admin", "developer", "developer"], entries.Select(e => e.ActorName));
        Assert.All(entries, e => Assert.Equal("Task", e.EntityType));
    }

    [Fact]
    public async Task Only_project_managers_and_admins_read_the_log()
    {
        var world = await WorldAsync();

        Assert.Equal(HttpStatusCode.OK, (await world.Org.Admin.Client.GetAsync(world.Activity)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Developer.Client.GetAsync(world.Activity)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Viewer.Client.GetAsync(world.Activity)).StatusCode);
    }

    [Fact]
    public async Task The_history_of_one_entity_can_be_requested()
    {
        var world = await WorldAsync();
        var first = await CreateTaskAsync(world, world.Developer, "First");
        await CreateTaskAsync(world, world.Developer, "Second");

        var history = await ReadAsync(world, world.Org.Admin, $"?entityId={first}");

        Assert.Equal(first, Assert.Single(history.Items).EntityId);
    }

    [Fact]
    public async Task Rejected_changes_leave_no_trace()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer, "Stays in ToDo");
        var before = (await ReadAsync(world, world.Org.Admin)).TotalCount;

        var invalid = await world.Developer.Client.PostJsonAsync($"{world.Tasks}/{task}/status", new ChangeTaskStatusRequest(TaskItemStatus.Done));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(before, (await ReadAsync(world, world.Org.Admin)).TotalCount);
    }

    [Fact]
    public async Task Sprint_and_project_lifecycle_and_membership_are_logged()
    {
        var world = await WorldAsync();
        var newcomer = await world.Org.AddMemberAsync("newcomer");
        await world.Org.Admin.Client.PostJsonAsync($"{world.Project}/members", new AddProjectMemberRequest(newcomer.Email, ProjectRole.Viewer));
        var sprint = await world.Org.Admin.Client.PostJsonAsync($"{world.Project}/sprints", new SprintRequest("Sprint 1", null, null, null));
        var sprintId = (await sprint.ReadAsync<ProjectFlow.Application.Sprints.SprintResponse>()).Id;
        await world.Org.Admin.Client.PostAsync($"{world.Project}/sprints/{sprintId}/start", null);
        await world.Org.Admin.Client.PostAsync($"{world.Project}/archive", null);

        var actions = (await ReadAsync(world, world.Org.Admin)).Items.Select(e => (e.EntityType, e.Action)).ToList();

        Assert.Contains(("Project", "MemberAdded"), actions);
        Assert.Contains(("Sprint", "Created"), actions);
        Assert.Contains(("Sprint", "Started"), actions);
        Assert.Equal(("Project", "Archived"), actions[0]);
    }

    [Fact]
    public async Task The_log_is_paged()
    {
        var world = await WorldAsync();
        for (var i = 0; i < 5; i++)
        {
            await CreateTaskAsync(world, world.Developer, $"Task {i}");
        }

        var firstPage = await ReadAsync(world, world.Org.Admin, "?page=1&pageSize=2");
        var lastPage = await ReadAsync(world, world.Org.Admin, $"?page={firstPage.TotalPages}&pageSize=2");
        var tooLarge = await world.Org.Admin.Client.GetAsync($"{world.Activity}?pageSize=101");

        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNextPage);
        Assert.True(firstPage.TotalCount >= 5);
        Assert.False(lastPage.HasNextPage);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [Fact]
    public async Task The_database_rejects_updates_and_deletes_of_the_log()
    {
        var world = await WorldAsync();
        await CreateTaskAsync(world, world.Developer, "Logged");
        await using var dbContext = api.CreateDbContext(world.Org.Id);

        var update = await Record.ExceptionAsync(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE activity_logs SET action = 'Tampered' WHERE organization_id = {world.Org.Id}"));
        var delete = await Record.ExceptionAsync(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM activity_logs WHERE organization_id = {world.Org.Id}"));

        Assert.Contains("insert-only", Assert.IsType<PostgresException>(update).MessageText);
        Assert.Contains("insert-only", Assert.IsType<PostgresException>(delete).MessageText);
        Assert.NotEmpty(await dbContext.ActivityLogs.ToListAsync());
    }

    // ---------- Helpers ----------

    private sealed record World(TestOrganization Org, Guid ProjectId, ApiUser Developer, ApiUser Viewer)
    {
        public string Project => Org.Project(ProjectId);

        public string Tasks => $"{Project}/tasks";

        public string Activity => $"{Project}/activity";
    }

    private async Task<World> WorldAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = (await org.CreateProjectAsync("ACT")).Id;
        var developer = await org.AddProjectUserAsync(project, "developer", ProjectRole.Developer);
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);
        return new World(org, project, developer, viewer);
    }

    private static async Task<Guid> CreateTaskAsync(World world, ApiUser reporter, string title)
    {
        var response = await reporter.Client.PostJsonAsync(
            world.Tasks,
            new CreateTaskRequest(TaskType.Task, title, null, TaskPriority.Medium, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<TaskResponse>()).Id;
    }

    private static async Task<PagedResponse<ActivityResponse>> ReadAsync(World world, ApiUser user, string query = "")
    {
        var response = await user.Client.GetAsync($"{world.Activity}{query}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<PagedResponse<ActivityResponse>>();
    }
}
