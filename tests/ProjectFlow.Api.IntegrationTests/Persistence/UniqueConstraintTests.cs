using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Users;
using static ProjectFlow.Api.IntegrationTests.Infrastructure.TestData;

namespace ProjectFlow.Api.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public class UniqueConstraintTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task User_email_is_unique()
    {
        var email = Email.Create($"dup-{Unique()}@example.com").Value;
        await SaveAsync(User.Create(email, "hash", "First", Now).Value);

        await DatabaseAssert.RejectedAsync(
            () => SaveAsync(User.Create(email, "hash", "Second", Now).Value),
            PostgresErrorCodes.UniqueViolation,
            "ix_users_email");
    }

    [Fact]
    public async Task Organization_slug_is_unique()
    {
        var slug = Slug.Create($"dup-{Unique()}").Value;
        var first = NewUser();
        var second = NewUser();
        await SaveAsync(first, Organization.Create("First", slug, first.Id, Now).Value);

        await DatabaseAssert.RejectedAsync(
            () => SaveAsync(second, Organization.Create("Second", slug, second.Id, Now).Value),
            PostgresErrorCodes.UniqueViolation,
            "ix_organizations_slug");
    }

    [Fact]
    public async Task Project_key_is_unique_within_an_organization()
    {
        var seeded = await api.SeedProjectAsync("WEB");
        var duplicate = Project.Create(seeded.OrganizationId, ProjectKey.Create("WEB").Value, "Duplicate", null, seeded.UserId, Now).Value;

        await DatabaseAssert.RejectedAsync(
            () => SaveAsync(duplicate),
            PostgresErrorCodes.UniqueViolation,
            "ix_projects_organization_id_key");
    }

    [Fact]
    public async Task Same_project_key_is_allowed_in_different_organizations()
    {
        await api.SeedProjectAsync("SHARED");

        var exception = await Record.ExceptionAsync(() => api.SeedProjectAsync("SHARED"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Label_name_is_unique_within_a_project_ignoring_case()
    {
        var project = await api.SeedProjectAsync();
        await InsertLabelAsync(project, "Backend");

        await DatabaseAssert.RejectedAsync(
            () => InsertLabelAsync(project, "BACKEND"),
            PostgresErrorCodes.UniqueViolation,
            "ix_labels_project_id_lower_name");
    }

    [Fact]
    public async Task Same_label_name_is_allowed_in_different_projects()
    {
        await InsertLabelAsync(await api.SeedProjectAsync(), "backend");

        var exception = await Record.ExceptionAsync(async () => await InsertLabelAsync(await api.SeedProjectAsync(), "backend"));

        Assert.Null(exception);
    }

    // Raw SQL: the domain and the use case already refuse duplicates, the index is the last line of defense.
    private async Task InsertLabelAsync(SeededProject project, string name)
    {
        await using var dbContext = api.CreateDbContext(organizationId: null);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO labels (id, project_id, organization_id, name, color) VALUES ({Guid.NewGuid()}, {project.ProjectId}, {project.OrganizationId}, {name}, '#1D76DB')");
    }

    private async Task SaveAsync(params object[] entities)
    {
        await using var dbContext = api.CreateDbContext(organizationId: null);
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }
}
