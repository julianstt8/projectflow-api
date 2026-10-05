using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Api.IntegrationTests.Infrastructure;

public sealed record SeededProject(Guid UserId, Guid OrganizationId, Guid ProjectId);

public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A unique suffix so tests never collide on unique columns (e-mail, slug).</summary>
    public static string Unique() => Guid.NewGuid().ToString("N")[..8];

    public static User NewUser() =>
        User.Create(Email.Create($"user-{Unique()}@example.com").Value, "hash", "Test User", Now).Value;

    public static Organization NewOrganization(User admin) =>
        Organization.Create("Test Org", Slug.Create($"org-{Unique()}").Value, admin.Id, Now).Value;

    /// <summary>Saves a user, an organization and a project with key <paramref name="key"/>.</summary>
    public static async Task<SeededProject> SeedProjectAsync(this ProjectFlowApiFactory api, string key = "PRJ")
    {
        var user = NewUser();
        var organization = NewOrganization(user);
        var project = Project.Create(organization.Id, ProjectKey.Create(key).Value, "Project", null, user.Id, Now).Value;

        await using var dbContext = api.CreateDbContext(organization.Id);
        dbContext.AddRange(user, organization, project);
        await dbContext.SaveChangesAsync();

        return new SeededProject(user.Id, organization.Id, project.Id);
    }
}
