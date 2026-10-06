using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.IntegrationTests.Projects;

[Collection(ApiCollection.Name)]
public class ProjectEndpointsTests(ProjectFlowApiFactory api)
{
    // ---------- Create ----------

    [Fact]
    public async Task Admin_creates_a_project_and_becomes_its_project_manager()
    {
        var org = await api.CreateOrganizationAsync();

        var response = await org.Admin.Client.PostJsonAsync(org.Projects, new CreateProjectRequest(" web ", "ProjectFlow Web", "Customer app"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ProjectSummaryResponse>();
        Assert.Equal("WEB", created.Key);
        Assert.Equal(ProjectRole.ProjectManager, created.MyRole);
        Assert.EndsWith($"{org.Projects}/{created.Id}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        var details = await (await org.Admin.Client.GetAsync($"{org.Projects}/{created.Id}")).ReadAsync<ProjectDetailsResponse>();
        Assert.Equal("Customer app", details.Description);
        var member = Assert.Single(details.Members);
        Assert.Equal(org.Admin.Id, member.UserId);
        Assert.Equal(ProjectRole.ProjectManager, member.Role);
    }

    [Fact]
    public async Task Project_keys_are_unique_per_organization_and_stay_reserved_after_delete()
    {
        var org = await api.CreateOrganizationAsync();
        var other = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("API");

        var duplicate = await org.Admin.Client.PostJsonAsync(org.Projects, new CreateProjectRequest("api", "Again", null));
        var otherOrganization = await other.Admin.Client.PostJsonAsync(other.Projects, new CreateProjectRequest("API", "Theirs", null));
        await org.Admin.Client.DeleteAsync($"{org.Projects}/{project.Id}");
        var afterDelete = await org.Admin.Client.PostJsonAsync(org.Projects, new CreateProjectRequest("API", "Reuse", null));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Project.KeyAlreadyTaken", await duplicate.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Created, otherOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Invalid_key_returns_400()
    {
        var org = await api.CreateOrganizationAsync();

        var response = await org.Admin.Client.PostJsonAsync(org.Projects, new CreateProjectRequest("1BAD-KEY", "Bad", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Project.KeyInvalid", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Only_organization_admins_create_projects()
    {
        var org = await api.CreateOrganizationAsync();
        var member = await org.AddMemberAsync("member");

        var response = await member.Client.PostJsonAsync(org.Projects, new CreateProjectRequest("NOPE", "Nope", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Visibility ----------

    [Fact]
    public async Task Admins_list_every_project_and_members_only_their_own()
    {
        var org = await api.CreateOrganizationAsync();
        var carla = await org.AddMemberAsync("carla");
        var visible = await org.CreateProjectAsync("VIS");
        var hidden = await org.CreateProjectAsync("HID");
        await org.AddProjectMemberAsync(visible.Id, carla, ProjectRole.Developer);

        var adminList = await (await org.Admin.Client.GetAsync(org.Projects)).ReadAsync<List<ProjectSummaryResponse>>();
        var carlaList = await (await carla.Client.GetAsync(org.Projects)).ReadAsync<List<ProjectSummaryResponse>>();

        Assert.Equal(["HID", "VIS"], adminList.Select(p => p.Key).Order());
        var carlasProject = Assert.Single(carlaList);
        Assert.Equal(visible.Id, carlasProject.Id);
        Assert.Equal(ProjectRole.Developer, carlasProject.MyRole);
        Assert.Equal(HttpStatusCode.NotFound, (await carla.Client.GetAsync($"{org.Projects}/{hidden.Id}")).StatusCode);
    }

    [Fact]
    public async Task Users_of_another_organization_cannot_see_the_projects()
    {
        var org = await api.CreateOrganizationAsync();
        var outsider = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("SEC");

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Admin.Client.GetAsync(org.Projects)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Admin.Client.GetAsync($"{org.Projects}/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Admin.Client.GetAsync($"{outsider.Projects}/{project.Id}")).StatusCode);
    }

    // ---------- Edit, archive, delete ----------

    [Fact]
    public async Task Project_manager_edits_the_project_but_a_developer_cannot()
    {
        var org = await api.CreateOrganizationAsync();
        var bruno = await org.AddMemberAsync("bruno");
        var carla = await org.AddMemberAsync("carla");
        var project = await org.CreateProjectAsync("EDT");
        await org.AddProjectMemberAsync(project.Id, bruno, ProjectRole.ProjectManager);
        await org.AddProjectMemberAsync(project.Id, carla, ProjectRole.Developer);

        var byManager = await bruno.Client.PutJsonAsync($"{org.Projects}/{project.Id}", new UpdateProjectRequest("Renamed", "New description"));
        var byDeveloper = await carla.Client.PutJsonAsync($"{org.Projects}/{project.Id}", new UpdateProjectRequest("Hacked", null));

        Assert.Equal(HttpStatusCode.NoContent, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byDeveloper.StatusCode);
        var details = await (await carla.Client.GetAsync($"{org.Projects}/{project.Id}")).ReadAsync<ProjectDetailsResponse>();
        Assert.Equal("Renamed", details.Name);
        Assert.Equal("New description", details.Description);
        Assert.Equal(
            [ProjectRole.ProjectManager, ProjectRole.ProjectManager, ProjectRole.Developer],
            details.Members.Select(member => member.Role));
    }

    [Fact]
    public async Task Archived_projects_are_read_only_until_unarchived()
    {
        var org = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("ARC");
        var url = $"{org.Projects}/{project.Id}";

        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.PostAsync($"{url}/archive", null)).StatusCode);
        var whileArchived = await org.Admin.Client.PutJsonAsync(url, new UpdateProjectRequest("Changed", null));
        var archiveAgain = await org.Admin.Client.PostAsync($"{url}/archive", null);
        var stillVisible = await (await org.Admin.Client.GetAsync(url)).ReadAsync<ProjectDetailsResponse>();
        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.PostAsync($"{url}/unarchive", null)).StatusCode);
        var afterUnarchive = await org.Admin.Client.PutJsonAsync(url, new UpdateProjectRequest("Changed", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, whileArchived.StatusCode);
        Assert.Equal("Project.Archived", await whileArchived.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, archiveAgain.StatusCode);
        Assert.True(stillVisible.IsArchived);
        Assert.Equal(HttpStatusCode.NoContent, afterUnarchive.StatusCode);
    }

    [Fact]
    public async Task Deleted_projects_disappear_and_only_admins_delete()
    {
        var org = await api.CreateOrganizationAsync();
        var bruno = await org.AddMemberAsync("bruno");
        var project = await org.CreateProjectAsync("DEL");
        await org.AddProjectMemberAsync(project.Id, bruno, ProjectRole.ProjectManager);
        var url = $"{org.Projects}/{project.Id}";

        var byManager = await bruno.Client.DeleteAsync(url);
        var byAdmin = await org.Admin.Client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Admin.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Admin.Client.DeleteAsync(url)).StatusCode);
        var list = await (await org.Admin.Client.GetAsync(org.Projects)).ReadAsync<List<ProjectSummaryResponse>>();
        Assert.DoesNotContain(list, p => p.Id == project.Id);
    }

    // ---------- Members ----------

    [Fact]
    public async Task Adding_a_member_gives_access_and_removing_it_takes_it_away()
    {
        var org = await api.CreateOrganizationAsync();
        var carla = await org.AddMemberAsync("carla");
        var project = await org.CreateProjectAsync("MEM");
        var url = $"{org.Projects}/{project.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await carla.Client.GetAsync(url)).StatusCode);

        var added = await org.Admin.Client.PostJsonAsync($"{url}/members", new AddProjectMemberRequest(carla.Email, ProjectRole.Viewer));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await carla.Client.GetAsync(url)).StatusCode);

        var promoted = await org.Admin.Client.PutJsonAsync($"{url}/members/{carla.Id}", new ChangeProjectMemberRoleRequest(ProjectRole.ProjectManager));
        Assert.Equal(HttpStatusCode.NoContent, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await carla.Client.PutJsonAsync(url, new UpdateProjectRequest("By Carla", null))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await org.Admin.Client.DeleteAsync($"{url}/members/{carla.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carla.Client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Only_organization_members_can_join_a_project()
    {
        var org = await api.CreateOrganizationAsync();
        var stranger = await api.CreateUserAsync("stranger");
        var project = await org.CreateProjectAsync("ORG");
        var url = $"{org.Projects}/{project.Id}/members";

        var notInOrganization = await org.Admin.Client.PostJsonAsync(url, new AddProjectMemberRequest(stranger.Email, ProjectRole.Developer));
        var unknown = await org.Admin.Client.PostJsonAsync(url, new AddProjectMemberRequest($"ghost-{TestData.Unique()}@example.com", ProjectRole.Developer));
        var alreadyMember = await org.Admin.Client.PostJsonAsync(url, new AddProjectMemberRequest(org.Admin.Email, ProjectRole.Developer));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, notInOrganization.StatusCode);
        Assert.Equal("Project.UserNotInOrganization", await notInOrganization.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Project.UserNotFound", await unknown.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, alreadyMember.StatusCode);
        Assert.Equal("Project.MemberAlreadyExists", await alreadyMember.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task The_last_project_manager_cannot_be_demoted_or_removed()
    {
        var org = await api.CreateOrganizationAsync();
        var project = await org.CreateProjectAsync("LPM");
        var url = $"{org.Projects}/{project.Id}/members/{org.Admin.Id}";

        var demote = await org.Admin.Client.PutJsonAsync(url, new ChangeProjectMemberRoleRequest(ProjectRole.Developer));
        var remove = await org.Admin.Client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, demote.StatusCode);
        Assert.Equal("Project.LastProjectManager", await demote.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, remove.StatusCode);
    }

    [Fact]
    public async Task Developers_cannot_manage_project_members()
    {
        var org = await api.CreateOrganizationAsync();
        var carla = await org.AddMemberAsync("carla");
        var diego = await org.AddMemberAsync("diego");
        var project = await org.CreateProjectAsync("DEV");
        await org.AddProjectMemberAsync(project.Id, carla, ProjectRole.Developer);

        var response = await carla.Client.PostJsonAsync(
            $"{org.Projects}/{project.Id}/members",
            new AddProjectMemberRequest(diego.Email, ProjectRole.Developer));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
