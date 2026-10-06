using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using static ProjectFlow.Domain.Projects.ProjectPermission;

namespace ProjectFlow.Api.IntegrationTests.Authorization;

/// <summary>
/// The full RBAC matrix of the PRD through the real HTTP pipeline (token → policy → database), for every
/// kind of user × every permission. Expected results are written out by hand, not derived from the code.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectAuthorizationTests(ProjectFlowApiFactory api) : IAsyncLifetime
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

    public static TheoryData<string, ProjectPermission, HttpStatusCode> Matrix()
    {
        ProjectPermission[] all = Enum.GetValues<ProjectPermission>();
        var allowed = new Dictionary<string, ProjectPermission[]>
        {
            ["organization admin (no project role)"] = all,
            ["project manager"] = all,
            ["developer"] = [ViewProject, CreateTasks, EditOwnTasks, Comment],
            ["viewer"] = [ViewProject],
        };

        var data = new TheoryData<string, ProjectPermission, HttpStatusCode>();
        foreach (var permission in all)
        {
            foreach (var (user, permissions) in allowed)
            {
                data.Add(user, permission, permissions.Contains(permission) ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
            }

            // Cannot see the project at all: same answer as a project that does not exist.
            data.Add("organization member without project role", permission, HttpStatusCode.NotFound);
            data.Add("user from another organization", permission, HttpStatusCode.NotFound);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Every_user_gets_exactly_the_permissions_of_the_PRD_matrix(string user, ProjectPermission permission, HttpStatusCode expected)
    {
        var response = await World.Users[user].Client.GetAsync(World.ProbeUrl(World.ProjectId, permission));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Hidden_projects_and_missing_projects_look_the_same()
    {
        var outsider = World.Users["organization member without project role"];

        var hidden = await outsider.Client.GetAsync(World.ProbeUrl(World.ProjectId, ViewProject));
        var missing = await outsider.Client.GetAsync(World.ProbeUrl(Guid.NewGuid(), ViewProject));

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Project.NotFound", await hidden.ReadErrorCodeAsync());
        Assert.Equal("Project.NotFound", await missing.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task A_project_cannot_be_reached_through_another_organizations_route()
    {
        var admin = World.Users["organization admin (no project role)"];

        // Ana is admin of her organization, but the project belongs to another one.
        var response = await admin.Client.GetAsync(World.ProbeUrl(World.OtherOrganizationProjectId, ViewProject));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleted_projects_are_not_found_even_for_admins()
    {
        var response = await World.Users["organization admin (no project role)"].Client
            .GetAsync(World.ProbeUrl(World.DeletedProjectId, ViewProject));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Leaving_the_organization_removes_access_to_its_projects()
    {
        var leaver = await World.Host.CreateUserAsync("leaver");
        await World.AddToOrganizationAsync(leaver);
        await World.AddToProjectAsync(leaver.Id, ProjectRole.Developer);
        Assert.Equal(HttpStatusCode.OK, (await leaver.Client.GetAsync(World.ProbeUrl(World.ProjectId, ViewProject))).StatusCode);

        await leaver.Client.DeleteAsync($"/api/organizations/{World.OrganizationId}/members/{leaver.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await leaver.Client.GetAsync(World.ProbeUrl(World.ProjectId, ViewProject))).StatusCode);
    }

    [Fact]
    public async Task Requests_without_a_token_get_401()
    {
        var anonymous = World.Host.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(World.ProbeUrl(World.ProjectId, ViewProject))).StatusCode);
    }

    /// <summary>One organization with a project and a user for every row of the matrix, created once.</summary>
    private sealed class Scenario
    {
        public required WebApplicationFactory<Program> Host { get; init; }

        public required ProjectFlowApiFactory Api { get; init; }

        public required Dictionary<string, ApiUser> Users { get; init; }

        public required Guid OrganizationId { get; init; }

        public required Guid ProjectId { get; init; }

        public required Guid DeletedProjectId { get; init; }

        public required Guid OtherOrganizationProjectId { get; init; }

        public string ProbeUrl(Guid projectId, ProjectPermission permission) =>
            $"/api/organizations/{OrganizationId}/projects/{projectId}/authorization-probe/{permission}";

        public async Task AddToOrganizationAsync(ApiUser user)
        {
            var admin = Users["organization admin (no project role)"];
            var response = await admin.Client.PostJsonAsync(
                $"/api/organizations/{OrganizationId}/members",
                new AddMemberRequest(user.Email, OrganizationRole.Member));
            response.EnsureSuccessStatusCode();
        }

        public async Task AddToProjectAsync(Guid userId, ProjectRole role)
        {
            await using var dbContext = Api.CreateDbContext(OrganizationId);
            var project = await dbContext.Projects.Include(p => p.Members).SingleAsync(p => p.Id == ProjectId);
            Assert.True(project.AddMember(userId, role).IsSuccess);
            await dbContext.SaveChangesAsync();
        }

        public static async Task<Scenario> CreateAsync(ProjectFlowApiFactory api)
        {
            // The probe controller exists only in this host. Users must be created here too:
            // in Development each host signs tokens with its own random key.
            var host = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ProjectAuthorizationProbeController).Assembly)));

            var ana = await host.CreateUserAsync("ana");
            var bruno = await host.CreateUserAsync("bruno");
            var carla = await host.CreateUserAsync("carla");
            var diego = await host.CreateUserAsync("diego");
            var elena = await host.CreateUserAsync("elena");
            var frank = await host.CreateUserAsync("frank");

            var organizationId = await CreateOrganizationAsync(ana);
            var otherOrganizationId = await CreateOrganizationAsync(frank);

            var users = new Dictionary<string, ApiUser>
            {
                ["organization admin (no project role)"] = ana,
                ["project manager"] = bruno,
                ["developer"] = carla,
                ["viewer"] = diego,
                ["organization member without project role"] = elena,
                ["user from another organization"] = frank,
            };

            var scenario = new Scenario
            {
                Host = host,
                Api = api,
                Users = users,
                OrganizationId = organizationId,
                ProjectId = Guid.Empty,
                DeletedProjectId = Guid.Empty,
                OtherOrganizationProjectId = Guid.Empty,
            };

            foreach (var member in new[] { bruno, carla, diego, elena })
            {
                await scenario.AddToOrganizationAsync(member);
            }

            // Projects are created through the domain until the project endpoints exist (#13).
            var project = Project.Create(organizationId, ProjectKey.Create("WEB").Value, "Web", null, bruno.Id, TestData.Now).Value;
            project.AddMember(carla.Id, ProjectRole.Developer);
            project.AddMember(diego.Id, ProjectRole.Viewer);
            var deleted = Project.Create(organizationId, ProjectKey.Create("OLD").Value, "Old", null, bruno.Id, TestData.Now).Value;
            deleted.Delete(TestData.Now);
            var foreign = Project.Create(otherOrganizationId, ProjectKey.Create("EXT").Value, "External", null, frank.Id, TestData.Now).Value;

            await using (var dbContext = api.CreateDbContext(organizationId: null))
            {
                dbContext.AddRange(project, deleted, foreign);
                await dbContext.SaveChangesAsync();
            }

            return new Scenario
            {
                Host = host,
                Api = api,
                Users = users,
                OrganizationId = organizationId,
                ProjectId = project.Id,
                DeletedProjectId = deleted.Id,
                OtherOrganizationProjectId = foreign.Id,
            };
        }

        private static async Task<Guid> CreateOrganizationAsync(ApiUser admin)
        {
            var response = await admin.Client.PostJsonAsync(
                "/api/organizations",
                new CreateOrganizationCommand("Org", $"org-{TestData.Unique()}"));
            response.EnsureSuccessStatusCode();
            return (await response.ReadAsync<OrganizationSummaryResponse>()).Id;
        }
    }
}
