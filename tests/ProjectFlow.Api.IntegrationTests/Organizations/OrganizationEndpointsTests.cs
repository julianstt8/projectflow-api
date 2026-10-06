using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Api.IntegrationTests.Organizations;

[Collection(ApiCollection.Name)]
public class OrganizationEndpointsTests(ProjectFlowApiFactory api)
{
    // ---------- Create and read ----------

    [Fact]
    public async Task Creator_becomes_admin_and_sees_the_organization()
    {
        var ana = await api.CreateUserAsync("ana");

        var created = await CreateOrganizationAsync(ana);

        Assert.Equal(OrganizationRole.Admin, created.Role);
        var mine = await (await ana.Client.GetAsync("/api/organizations")).ReadAsync<List<OrganizationSummaryResponse>>();
        Assert.Contains(mine, organization => organization.Id == created.Id && organization.Role == OrganizationRole.Admin);

        var details = await (await ana.Client.GetAsync($"/api/organizations/{created.Id}")).ReadAsync<OrganizationDetailsResponse>();
        var member = Assert.Single(details.Members);
        Assert.Equal(ana.Id, member.UserId);
        Assert.Equal(ana.Email, member.Email);
    }

    [Fact]
    public async Task Create_returns_201_with_location()
    {
        var ana = await api.CreateUserAsync("ana");

        var response = await ana.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("Acme", $"acme-{TestData.Unique()}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<OrganizationSummaryResponse>();
        Assert.EndsWith($"/api/organizations/{created.Id}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Slug_already_taken_returns_409()
    {
        var ana = await api.CreateUserAsync("ana");
        var slug = $"taken-{TestData.Unique()}";
        await ana.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("First", slug));

        var response = await ana.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("Second", slug));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Organization.SlugAlreadyTaken", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Invalid_slug_returns_400()
    {
        var ana = await api.CreateUserAsync("ana");

        var response = await ana.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("Acme", "Not a slug!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Organization.SlugInvalid", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Users_only_list_their_own_organizations()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var anas = await CreateOrganizationAsync(ana);

        var brunosList = await (await bruno.Client.GetAsync("/api/organizations")).ReadAsync<List<OrganizationSummaryResponse>>();

        Assert.DoesNotContain(brunosList, organization => organization.Id == anas.Id);
    }

    [Fact]
    public async Task Requests_without_a_token_get_401()
    {
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/organizations/{Guid.NewGuid()}")).StatusCode);
    }

    // ---------- Access rules ----------

    [Fact]
    public async Task Non_members_get_404_as_if_the_organization_did_not_exist()
    {
        var ana = await api.CreateUserAsync("ana");
        var outsider = await api.CreateUserAsync("outsider");
        var organization = await CreateOrganizationAsync(ana);

        var existing = await outsider.Client.GetAsync($"/api/organizations/{organization.Id}");
        var missing = await outsider.Client.GetAsync($"/api/organizations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.ReadErrorCodeAsync(), await existing.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Members_can_read_but_only_admins_manage_members()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var carla = await api.CreateUserAsync("carla");
        var organization = await CreateOrganizationAsync(ana);
        await AddMemberAsync(ana, organization.Id, bruno.Email, OrganizationRole.Member);

        Assert.Equal(HttpStatusCode.OK, (await bruno.Client.GetAsync($"/api/organizations/{organization.Id}")).StatusCode);

        var add = await bruno.Client.PostJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddMemberRequest(carla.Email, OrganizationRole.Member));
        var promote = await bruno.Client.PutJsonAsync(
            $"/api/organizations/{organization.Id}/members/{bruno.Id}",
            new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
    }

    // ---------- Member management ----------

    [Fact]
    public async Task Admin_adds_a_member_who_then_sees_the_organization()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var organization = await CreateOrganizationAsync(ana);

        var response = await ana.Client.PostJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddMemberRequest(bruno.Email.ToUpperInvariant(), OrganizationRole.Member));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var member = await response.ReadAsync<OrganizationMemberResponse>();
        Assert.Equal(bruno.Id, member.UserId);
        Assert.Equal(OrganizationRole.Member, member.Role);
        var brunosList = await (await bruno.Client.GetAsync("/api/organizations")).ReadAsync<List<OrganizationSummaryResponse>>();
        Assert.Contains(brunosList, o => o.Id == organization.Id && o.Role == OrganizationRole.Member);
    }

    [Fact]
    public async Task Adding_an_unknown_email_or_an_existing_member_fails()
    {
        var ana = await api.CreateUserAsync("ana");
        var organization = await CreateOrganizationAsync(ana);

        var unknown = await ana.Client.PostJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddMemberRequest($"nobody-{TestData.Unique()}@example.com", OrganizationRole.Member));
        var existing = await ana.Client.PostJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddMemberRequest(ana.Email, OrganizationRole.Member));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Organization.UserNotFound", await unknown.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, existing.StatusCode);
        Assert.Equal("Organization.MemberAlreadyExists", await existing.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task The_last_admin_cannot_be_demoted_or_removed()
    {
        var ana = await api.CreateUserAsync("ana");
        var organization = await CreateOrganizationAsync(ana);

        var demote = await ana.Client.PutJsonAsync(
            $"/api/organizations/{organization.Id}/members/{ana.Id}",
            new ChangeMemberRoleRequest(OrganizationRole.Member));
        var leave = await ana.Client.DeleteAsync($"/api/organizations/{organization.Id}/members/{ana.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, demote.StatusCode);
        Assert.Equal("Organization.LastAdmin", await demote.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, leave.StatusCode);
        Assert.Equal("Organization.LastAdmin", await leave.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Admin_can_hand_over_the_admin_role_and_step_down()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var organization = await CreateOrganizationAsync(ana);
        await AddMemberAsync(ana, organization.Id, bruno.Email, OrganizationRole.Member);

        var promote = await ana.Client.PutJsonAsync(
            $"/api/organizations/{organization.Id}/members/{bruno.Id}",
            new ChangeMemberRoleRequest(OrganizationRole.Admin));
        var stepDown = await ana.Client.PutJsonAsync(
            $"/api/organizations/{organization.Id}/members/{ana.Id}",
            new ChangeMemberRoleRequest(OrganizationRole.Member));

        Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, stepDown.StatusCode);
        var details = await (await bruno.Client.GetAsync($"/api/organizations/{organization.Id}")).ReadAsync<OrganizationDetailsResponse>();
        Assert.Equal(OrganizationRole.Admin, details.Members.Single(m => m.UserId == bruno.Id).Role);
        Assert.Equal(OrganizationRole.Member, details.Members.Single(m => m.UserId == ana.Id).Role);
    }

    [Fact]
    public async Task A_member_can_leave_but_cannot_remove_others()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var carla = await api.CreateUserAsync("carla");
        var organization = await CreateOrganizationAsync(ana);
        await AddMemberAsync(ana, organization.Id, bruno.Email, OrganizationRole.Member);
        await AddMemberAsync(ana, organization.Id, carla.Email, OrganizationRole.Member);

        var removeOther = await bruno.Client.DeleteAsync($"/api/organizations/{organization.Id}/members/{carla.Id}");
        var leave = await bruno.Client.DeleteAsync($"/api/organizations/{organization.Id}/members/{bruno.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, removeOther.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bruno.Client.GetAsync($"/api/organizations/{organization.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_removes_a_member()
    {
        var ana = await api.CreateUserAsync("ana");
        var bruno = await api.CreateUserAsync("bruno");
        var organization = await CreateOrganizationAsync(ana);
        await AddMemberAsync(ana, organization.Id, bruno.Email, OrganizationRole.Member);

        var response = await ana.Client.DeleteAsync($"/api/organizations/{organization.Id}/members/{bruno.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var details = await (await ana.Client.GetAsync($"/api/organizations/{organization.Id}")).ReadAsync<OrganizationDetailsResponse>();
        Assert.DoesNotContain(details.Members, member => member.UserId == bruno.Id);
    }

    private static async Task<OrganizationSummaryResponse> CreateOrganizationAsync(ApiUser user)
    {
        var response = await user.Client.PostJsonAsync("/api/organizations", new CreateOrganizationCommand("Acme", $"acme-{TestData.Unique()}"));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<OrganizationSummaryResponse>();
    }

    private static async Task AddMemberAsync(ApiUser admin, Guid organizationId, string email, OrganizationRole role)
    {
        var response = await admin.Client.PostJsonAsync($"/api/organizations/{organizationId}/members", new AddMemberRequest(email, role));
        response.EnsureSuccessStatusCode();
    }
}
