using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Api.IntegrationTests.Infrastructure.TestData;

namespace ProjectFlow.Api.IntegrationTests.Persistence;

/// <summary>
/// Two requests working at the same time can both pass the domain checks, because each one only sees
/// the data it loaded. These tests simulate that with two contexts and prove the database stops it.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Two_tasks_created_at_the_same_time_never_get_the_same_number()
    {
        var seeded = await api.SeedProjectAsync();
        await using var first = api.CreateDbContext(seeded.OrganizationId);
        await using var second = api.CreateDbContext(seeded.OrganizationId);
        var projectInFirst = await first.Projects.SingleAsync(project => project.Id == seeded.ProjectId);
        var projectInSecond = await second.Projects.SingleAsync(project => project.Id == seeded.ProjectId);

        first.Tasks.Add(NewTask(projectInFirst, seeded.UserId));
        second.Tasks.Add(NewTask(projectInSecond, seeded.UserId));
        await first.SaveChangesAsync();

        // Both tasks took number 1; the second save is rejected (xmin concurrency token on the
        // project or the unique (project_id, number) index, whichever PostgreSQL checks first).
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());

        await using var verify = api.CreateDbContext(seeded.OrganizationId);
        var numbers = await verify.Tasks.Where(task => task.ProjectId == seeded.ProjectId).Select(task => task.Number).ToListAsync();
        Assert.Equal([1], numbers);
    }

    [Fact]
    public async Task Two_sprints_started_at_the_same_time_leave_only_one_active()
    {
        var seeded = await api.SeedProjectAsync();
        Guid sprintA, sprintB;
        await using (var setup = api.CreateDbContext(seeded.OrganizationId))
        {
            var project = await setup.Projects.SingleAsync(p => p.Id == seeded.ProjectId);
            var a = Sprint.Create(project, "Sprint A", null, null, null, Now).Value;
            var b = Sprint.Create(project, "Sprint B", null, null, null, Now).Value;
            setup.Sprints.AddRange(a, b);
            await setup.SaveChangesAsync();
            (sprintA, sprintB) = (a.Id, b.Id);
        }

        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        await using var first = api.CreateDbContext(seeded.OrganizationId);
        await using var second = api.CreateDbContext(seeded.OrganizationId);
        var sprintsInFirst = await first.Sprints.Where(s => s.ProjectId == seeded.ProjectId).ToListAsync();
        var sprintsInSecond = await second.Sprints.Where(s => s.ProjectId == seeded.ProjectId).ToListAsync();

        // Each request sees no active sprint, so the domain rule (RF-05) lets both start one.
        Assert.True(sprintsInFirst.Single(s => s.Id == sprintA).Start(sprintsInFirst, today).IsSuccess);
        Assert.True(sprintsInSecond.Single(s => s.Id == sprintB).Start(sprintsInSecond, today).IsSuccess);
        await first.SaveChangesAsync();

        await DatabaseAssert.RejectedAsync(
            () => second.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation,
            "ix_sprints_project_id_active");
    }

    private static TaskItem NewTask(ProjectFlow.Domain.Projects.Project project, Guid reporterId) =>
        TaskItem.Create(project, reporterId, TaskType.Task, "Concurrent task", null, TaskPriority.Medium, Now).Value;
}
