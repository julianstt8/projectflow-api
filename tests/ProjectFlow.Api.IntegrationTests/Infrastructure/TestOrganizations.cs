using ProjectFlow.Api.Controllers;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.IntegrationTests.Infrastructure;

/// <summary>An organization created through the API, with its admin, ready to add members and projects.</summary>
public sealed record TestOrganization(ApiUser Admin, Guid Id, ProjectFlowApiFactory Api)
{
    public string Projects => $"/api/organizations/{Id}/projects";

    public string Project(Guid projectId) => $"{Projects}/{projectId}";

    public async Task<ApiUser> AddMemberAsync(string name)
    {
        var user = await Api.CreateUserAsync(name);
        var response = await Admin.Client.PostJsonAsync($"/api/organizations/{Id}/members", new AddMemberRequest(user.Email, OrganizationRole.Member));
        response.EnsureSuccessStatusCode();
        return user;
    }

    public async Task<ProjectSummaryResponse> CreateProjectAsync(string key)
    {
        var response = await Admin.Client.PostJsonAsync(Projects, new CreateProjectRequest(key, $"Project {key}", null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProjectSummaryResponse>();
    }

    public async Task AddProjectMemberAsync(Guid projectId, ApiUser user, ProjectRole role)
    {
        var response = await Admin.Client.PostJsonAsync($"{Project(projectId)}/members", new AddProjectMemberRequest(user.Email, role));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Adds a new organization member with the given role in the project.</summary>
    public async Task<ApiUser> AddProjectUserAsync(Guid projectId, string name, ProjectRole role)
    {
        var user = await AddMemberAsync(name);
        await AddProjectMemberAsync(projectId, user, role);
        return user;
    }
}

public static class TestOrganizations
{
    public static async Task<TestOrganization> CreateOrganizationAsync(this ProjectFlowApiFactory api)
    {
        var admin = await api.CreateUserAsync("admin");
        var response = await admin.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("Org", $"org-{TestData.Unique()}"));
        response.EnsureSuccessStatusCode();
        return new TestOrganization(admin, (await response.ReadAsync<OrganizationSummaryResponse>()).Id, api);
    }
}
