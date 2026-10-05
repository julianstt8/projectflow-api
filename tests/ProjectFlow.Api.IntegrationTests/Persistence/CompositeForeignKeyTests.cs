using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests.Persistence;

/// <summary>
/// The domain never creates a child with another organization's id, so these tests write raw SQL
/// to prove the database rejects it anyway (defense in depth against bugs or manual edits).
/// </summary>
[Collection(ApiCollection.Name)]
public class CompositeForeignKeyTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Sprint_cannot_point_to_a_project_of_another_organization()
    {
        var (project, otherOrganizationId) = await SeedTwoOrganizationsAsync();

        await DatabaseAssert.RejectedAsync(
            () => ExecuteAsync(
                db => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO sprints (id, project_id, organization_id, name, status) VALUES ({Guid.NewGuid()}, {project.ProjectId}, {otherOrganizationId}, 'Sprint', 'Planned')")),
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_sprints_projects_project_id_organization_id");
    }

    [Fact]
    public async Task Epic_cannot_point_to_a_project_of_another_organization()
    {
        var (project, otherOrganizationId) = await SeedTwoOrganizationsAsync();

        await DatabaseAssert.RejectedAsync(
            () => ExecuteAsync(
                db => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO epics (id, project_id, organization_id, name, status) VALUES ({Guid.NewGuid()}, {project.ProjectId}, {otherOrganizationId}, 'Epic', 'Open')")),
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_epics_projects_project_id_organization_id");
    }

    [Fact]
    public async Task Label_cannot_point_to_a_project_of_another_organization()
    {
        var (project, otherOrganizationId) = await SeedTwoOrganizationsAsync();

        await DatabaseAssert.RejectedAsync(
            () => ExecuteAsync(
                db => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO labels (id, project_id, organization_id, name, color) VALUES ({Guid.NewGuid()}, {project.ProjectId}, {otherOrganizationId}, 'label', '#000000')")),
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_labels_projects_project_id_organization_id");
    }

    [Fact]
    public async Task Task_cannot_point_to_a_project_of_another_organization()
    {
        var (project, otherOrganizationId) = await SeedTwoOrganizationsAsync();

        await DatabaseAssert.RejectedAsync(
            () => ExecuteAsync(
                db => db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO tasks (id, project_id, organization_id, number, reporter_id, title, type, priority, status, created_at, updated_at)
                    VALUES ({Guid.NewGuid()}, {project.ProjectId}, {otherOrganizationId}, 1, {project.UserId}, 'Task', 'Task', 'Low', 'ToDo', now(), now())
                    """)),
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_tasks_projects_project_id_organization_id");
    }

    private async Task<(SeededProject Project, Guid OtherOrganizationId)> SeedTwoOrganizationsAsync()
    {
        var project = await api.SeedProjectAsync();
        var other = await api.SeedProjectAsync();
        return (project, other.OrganizationId);
    }

    private async Task ExecuteAsync(Func<Microsoft.EntityFrameworkCore.DbContext, Task> action)
    {
        await using var dbContext = api.CreateDbContext(organizationId: null);
        await action(dbContext);
    }
}
