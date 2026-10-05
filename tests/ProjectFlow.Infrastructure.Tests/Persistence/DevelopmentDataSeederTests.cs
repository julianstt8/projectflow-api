using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;
using ProjectFlow.Infrastructure.Authentication;
using ProjectFlow.Infrastructure.Persistence;
using ProjectFlow.Infrastructure.Persistence.Seeding;

namespace ProjectFlow.Infrastructure.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class DevelopmentDataSeederTests(PostgreSqlFixture database) : IAsyncLifetime
{
    private static readonly PasswordHasher Hasher = new();

    // Seeding is shared by every test of the class; the first run inserts, later runs must be no-ops.
    public async Task InitializeAsync() => await SeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seeding_twice_inserts_nothing_the_second_time()
    {
        var usersBefore = await CountUsersAsync();

        var inserted = await SeedAsync();

        Assert.False(inserted);
        Assert.Equal(usersBefore, await CountUsersAsync());
    }

    [Fact]
    public async Task Seeded_users_have_fake_emails_and_hashed_passwords()
    {
        await using var dbContext = database.CreateDbContext(organizationId: null);
        var seededEmails = new[] { "ana.admin", "bruno.pm", "carla.dev", "diego.dev", "elena.viewer", "frank.admin", "grace.dev" }
            .Select(name => Email.Create($"{name}@example.com").Value)
            .ToList();
        var users = await dbContext.Users.Where(user => seededEmails.Contains(user.Email)).ToListAsync();

        Assert.Equal(7, users.Count);
        Assert.All(users, user =>
        {
            Assert.EndsWith("@example.com", user.Email.Value);
            Assert.NotEqual(DevelopmentDataSeeder.Password, user.PasswordHash);
            Assert.True(Hasher.Verify(DevelopmentDataSeeder.Password, user.PasswordHash));
        });
    }

    [Fact]
    public async Task Seed_covers_every_organization_and_project_role()
    {
        var acme = await GetOrganizationAsync(DevelopmentDataSeeder.AcmeSlug);
        var globex = await GetOrganizationAsync(DevelopmentDataSeeder.GlobexSlug);

        Assert.Equal(
            [OrganizationRole.Admin, OrganizationRole.Member],
            acme.Members.Select(member => member.Role).Distinct().Order());
        Assert.Equal(3, globex.Members.Count);

        var sharedMember = acme.Members.Select(member => member.UserId)
            .Intersect(globex.Members.Select(member => member.UserId));
        Assert.Single(sharedMember);

        await using var dbContext = database.CreateDbContext(acme.Id);
        var projectRoles = await dbContext.Projects
            .SelectMany(project => project.Members.Select(member => member.Role))
            .Distinct()
            .ToListAsync();

        Assert.Equal(
            [ProjectRole.ProjectManager, ProjectRole.Developer, ProjectRole.Viewer],
            projectRoles.Order());
    }

    [Fact]
    public async Task Seed_has_a_realistic_sprint_cycle_with_tasks_in_every_status()
    {
        var acme = await GetOrganizationAsync(DevelopmentDataSeeder.AcmeSlug);
        await using var dbContext = database.CreateDbContext(acme.Id);

        var web = await dbContext.Projects.SingleAsync(project => project.Key == ProjectKey.Create("WEB").Value);
        var sprintStatuses = await dbContext.Sprints
            .Where(sprint => sprint.ProjectId == web.Id)
            .Select(sprint => sprint.Status)
            .ToListAsync();
        var taskStatuses = await dbContext.Tasks
            .Where(task => task.ProjectId == web.Id)
            .Select(task => task.Status)
            .Distinct()
            .ToListAsync();

        Assert.Equal(Enum.GetValues<SprintStatus>(), sprintStatuses.Order());
        Assert.Equal(Enum.GetValues<TaskItemStatus>(), taskStatuses.Order());
        Assert.True(await dbContext.Comments.AnyAsync());
        Assert.True(await dbContext.Labels.AnyAsync());

        var deletedTasks = await dbContext.Tasks
            .IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter])
            .CountAsync(task => task.DeletedAt != null);
        Assert.Equal(1, deletedTasks);
    }

    private async Task<bool> SeedAsync()
    {
        await using var dbContext = database.CreateDbContext(organizationId: null);
        var seeder = new DevelopmentDataSeeder(
            dbContext,
            Hasher,
            TimeProvider.System,
            NullLogger<DevelopmentDataSeeder>.Instance);

        return await seeder.SeedAsync();
    }

    private async Task<int> CountUsersAsync()
    {
        await using var dbContext = database.CreateDbContext(organizationId: null);
        return await dbContext.Users.CountAsync();
    }

    private async Task<Organization> GetOrganizationAsync(string slug)
    {
        await using var dbContext = database.CreateDbContext(organizationId: null);
        var value = Slug.Create(slug).Value;

        return await dbContext.Organizations
            .Include(organization => organization.Members)
            .SingleAsync(organization => organization.Slug == value);
    }
}
