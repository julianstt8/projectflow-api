using Microsoft.EntityFrameworkCore;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;
using ProjectFlow.Infrastructure.Persistence;

namespace ProjectFlow.Infrastructure.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class OrganizationIsolationTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Each_organization_sees_only_its_own_data()
    {
        var acme = await SeedOrganizationAsync("acme");
        var globex = await SeedOrganizationAsync("globex");

        await using var dbContext = database.CreateDbContext(acme.OrganizationId);

        AssertOnlyOwnData(await dbContext.Projects.ToListAsync(), project => project.OrganizationId, acme.OrganizationId);
        AssertOnlyOwnData(await dbContext.Sprints.ToListAsync(), sprint => sprint.OrganizationId, acme.OrganizationId);
        AssertOnlyOwnData(await dbContext.Epics.ToListAsync(), epic => epic.OrganizationId, acme.OrganizationId);
        AssertOnlyOwnData(await dbContext.Labels.ToListAsync(), label => label.OrganizationId, acme.OrganizationId);
        AssertOnlyOwnData(await dbContext.Tasks.ToListAsync(), task => task.OrganizationId, acme.OrganizationId);
        AssertOnlyOwnData(await dbContext.Comments.ToListAsync(), comment => comment.OrganizationId, acme.OrganizationId);
        Assert.DoesNotContain(await dbContext.Projects.Select(project => project.Id).ToListAsync(), id => id == globex.ProjectId);
    }

    [Fact]
    public async Task Data_of_another_organization_cannot_be_found_by_id()
    {
        var acme = await SeedOrganizationAsync("acme");
        var globex = await SeedOrganizationAsync("globex");

        await using var dbContext = database.CreateDbContext(acme.OrganizationId);

        Assert.Null(await dbContext.Projects.FirstOrDefaultAsync(project => project.Id == globex.ProjectId));
        Assert.Null(await dbContext.Tasks.FirstOrDefaultAsync(task => task.Id == globex.TaskId));
        Assert.Null(await dbContext.Comments.FirstOrDefaultAsync(comment => comment.TaskId == globex.TaskId));
    }

    [Fact]
    public async Task Without_a_current_organization_no_organization_data_is_returned()
    {
        await SeedOrganizationAsync("acme");

        await using var dbContext = database.CreateDbContext(organizationId: null);

        Assert.False(await dbContext.Projects.AnyAsync());
        Assert.False(await dbContext.Tasks.AnyAsync());
        Assert.False(await dbContext.Comments.AnyAsync());
    }

    [Fact]
    public async Task Aggregates_round_trip_with_their_children()
    {
        var acme = await SeedOrganizationAsync("acme");

        await using var dbContext = database.CreateDbContext(acme.OrganizationId);
        var project = await dbContext.Projects.Include(p => p.Members).SingleAsync(p => p.Id == acme.ProjectId);
        var task = await dbContext.Tasks.Include(t => t.Labels).SingleAsync(t => t.Id == acme.TaskId);

        Assert.Equal("PRJ", project.Key.Value);
        Assert.Equal(2, project.NextTaskNumber);
        Assert.Equal(ProjectRole.ProjectManager, Assert.Single(project.Members).Role);
        Assert.Equal(1, task.Number);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Single(task.Labels);
    }

    [Fact]
    public async Task Soft_deleted_rows_are_hidden_but_stay_isolated_when_the_filter_is_ignored()
    {
        var acme = await SeedOrganizationAsync("acme");
        var globex = await SeedOrganizationAsync("globex");

        await using (var dbContext = database.CreateDbContext(acme.OrganizationId))
        {
            var task = await dbContext.Tasks.SingleAsync(t => t.Id == acme.TaskId);
            var project = await dbContext.Projects.SingleAsync(p => p.Id == acme.ProjectId);
            task.Delete(Now);
            project.Delete(Now);
            await dbContext.SaveChangesAsync();
        }

        await using var acmeContext = database.CreateDbContext(acme.OrganizationId);
        Assert.Null(await acmeContext.Tasks.FirstOrDefaultAsync(t => t.Id == acme.TaskId));
        Assert.Null(await acmeContext.Projects.FirstOrDefaultAsync(p => p.Id == acme.ProjectId));

        var deletedTask = await acmeContext.Tasks
            .IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter])
            .SingleAsync(t => t.Id == acme.TaskId);
        Assert.True(deletedTask.IsDeleted);

        await using var globexContext = database.CreateDbContext(globex.OrganizationId);
        Assert.Null(await globexContext.Tasks
            .IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter])
            .FirstOrDefaultAsync(t => t.Id == acme.TaskId));
    }

    private static void AssertOnlyOwnData<T>(IReadOnlyCollection<T> rows, Func<T, Guid> organizationOf, Guid organizationId)
    {
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(organizationId, organizationOf(row)));
    }

    private sealed record SeededOrganization(Guid OrganizationId, Guid ProjectId, Guid TaskId);

    /// <summary>Creates a full organization (user, project, sprint, epic, label, task, comment) with a unique slug.</summary>
    private async Task<SeededOrganization> SeedOrganizationAsync(string name)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = User.Create(Email.Create($"{name}-{suffix}@example.com").Value, "hash", "Test User", Now).Value;
        var organization = Organization.Create(name, Slug.Create($"{name}-{suffix}").Value, user.Id, Now).Value;
        var project = Project.Create(organization.Id, ProjectKey.Create("PRJ").Value, "ProjectFlow", null, user.Id, Now).Value;
        var sprint = Sprint.Create(project, "Sprint 1", null, null, null, Now).Value;
        var epic = Epic.Create(project, "Authentication", null, Now).Value;
        var label = Label.Create(project, "backend", "#1D76DB", Now).Value;
        var task = TaskItem.Create(project, user.Id, TaskType.Story, "Implement login", null, TaskPriority.High, Now).Value;
        task.AddLabel(label, Now);
        var comment = Comment.Create(task, user.Id, "Looks good", Now).Value;

        await using var dbContext = database.CreateDbContext(organization.Id);
        dbContext.AddRange(user, organization, project, sprint, epic, label, task, comment);
        await dbContext.SaveChangesAsync();

        return new SeededOrganization(organization.Id, project.Id, task.Id);
    }
}
