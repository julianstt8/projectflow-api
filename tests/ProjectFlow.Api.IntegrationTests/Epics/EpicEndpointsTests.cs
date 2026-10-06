using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Epics;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Epics;

[Collection(ApiCollection.Name)]
public class EpicEndpointsTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Project_manager_creates_an_epic_and_viewers_see_it()
    {
        var (org, project) = await ProjectAsync();
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);

        var response = await org.Admin.Client.PostJsonAsync(Epics(org, project), new EpicRequest(" Authentication ", "Login and sign-up"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<EpicResponse>();
        Assert.Equal("Authentication", created.Name);
        Assert.Equal(EpicStatus.Open, created.Status);
        var read = await (await viewer.Client.GetAsync(response.Headers.Location)).ReadAsync<EpicResponse>();
        Assert.Equal("Login and sign-up", read.Description);
    }

    [Theory]
    [InlineData(ProjectRole.Developer)]
    [InlineData(ProjectRole.Viewer)]
    public async Task Only_admins_and_project_managers_manage_epics(ProjectRole role)
    {
        var (org, project) = await ProjectAsync();
        var user = await org.AddProjectUserAsync(project, "user", role);
        var epic = await CreateEpicAsync(org, project, "Existing");

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostJsonAsync(Epics(org, project), new EpicRequest("New", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"{Epics(org, project)}/{epic.Id}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync(Epics(org, project))).StatusCode);
    }

    [Fact]
    public async Task Empty_name_is_rejected()
    {
        var (org, project) = await ProjectAsync();

        var response = await org.Admin.Client.PostJsonAsync(Epics(org, project), new EpicRequest(" ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Epics_are_updated_closed_and_reopened()
    {
        var (org, project) = await ProjectAsync();
        var epic = await CreateEpicAsync(org, project, "Dashboard");
        var url = $"{Epics(org, project)}/{epic.Id}";

        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.PutJsonAsync(url, new EpicRequest("Manager dashboard", "KPIs"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.PostAsync($"{url}/close", null)).StatusCode);
        var closeAgain = await org.Admin.Client.PostAsync($"{url}/close", null);
        var closed = await (await org.Admin.Client.GetAsync(url)).ReadAsync<EpicResponse>();
        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.PostAsync($"{url}/reopen", null)).StatusCode);
        var reopenAgain = await org.Admin.Client.PostAsync($"{url}/reopen", null);

        Assert.Equal("Manager dashboard", closed.Name);
        Assert.Equal(EpicStatus.Closed, closed.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closeAgain.StatusCode);
        Assert.Equal("Epic.AlreadyClosed", await closeAgain.ReadErrorCodeAsync());
        Assert.Equal("Epic.AlreadyOpen", await reopenAgain.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Epics_show_their_progress_and_open_ones_come_first()
    {
        var (org, project) = await ProjectAsync();
        var closed = await CreateEpicAsync(org, project, "A closed epic");
        var open = await CreateEpicAsync(org, project, "Z open epic");
        await api.AddTaskAsync(org.Id, project, open.Id, TaskItemStatus.Done);
        await api.AddTaskAsync(org.Id, project, open.Id, TaskItemStatus.InProgress);
        await api.AddTaskAsync(org.Id, project, open.Id, TaskItemStatus.Done, deleted: true);
        await org.Admin.Client.PostAsync($"{Epics(org, project)}/{closed.Id}/close", null);

        var epics = await (await org.Admin.Client.GetAsync(Epics(org, project))).ReadAsync<List<EpicResponse>>();

        Assert.Equal([open.Id, closed.Id], epics.Select(epic => epic.Id));
        Assert.Equal(2, epics[0].TaskCount);
        Assert.Equal(1, epics[0].DoneTaskCount);
        Assert.Equal(0, epics[1].TaskCount);
    }

    [Fact]
    public async Task Archived_projects_accept_no_epic_changes()
    {
        var (org, project) = await ProjectAsync();
        var epic = await CreateEpicAsync(org, project, "Before archive");
        await org.Admin.Client.PostAsync($"{org.Project(project)}/archive", null);

        var create = await org.Admin.Client.PostJsonAsync(Epics(org, project), new EpicRequest("After archive", null));
        var close = await org.Admin.Client.PostAsync($"{Epics(org, project)}/{epic.Id}/close", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode);
        Assert.Equal("Project.Archived", await create.ReadErrorCodeAsync());
        Assert.Equal("Project.Archived", await close.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task An_epic_cannot_be_reached_through_another_project()
    {
        var (org, project) = await ProjectAsync();
        var other = (await org.CreateProjectAsync("OTH")).Id;
        var epic = await CreateEpicAsync(org, project, "Mine");

        var read = await org.Admin.Client.GetAsync($"{Epics(org, other)}/{epic.Id}");
        var close = await org.Admin.Client.PostAsync($"{Epics(org, other)}/{epic.Id}/close", null);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("Epic.NotFound", await read.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, close.StatusCode);
    }

    private async Task<(TestOrganization Org, Guid ProjectId)> ProjectAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("EPC");
        return (org, project.Id);
    }

    private static string Epics(TestOrganization org, Guid projectId) => $"{org.Project(projectId)}/epics";

    private static async Task<EpicResponse> CreateEpicAsync(TestOrganization org, Guid projectId, string name)
    {
        var response = await org.Admin.Client.PostJsonAsync(Epics(org, projectId), new EpicRequest(name, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<EpicResponse>();
    }
}
