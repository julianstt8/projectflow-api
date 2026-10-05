using Microsoft.EntityFrameworkCore;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public class MigrationTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Api_startup_applies_every_migration()
    {
        await using var dbContext = api.CreateDbContext(organizationId: null);

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        var pending = await dbContext.Database.GetPendingMigrationsAsync();

        Assert.Equal(dbContext.Database.GetMigrations(), applied);
        Assert.Empty(pending);
    }
}
