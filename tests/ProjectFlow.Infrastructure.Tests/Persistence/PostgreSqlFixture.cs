using Microsoft.EntityFrameworkCore;
using ProjectFlow.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ProjectFlow.Infrastructure.Tests.Persistence;

/// <summary>Starts a throwaway PostgreSQL in Docker once per test run and applies the migrations.</summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateDbContext(organizationId: null);
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a context that works inside <paramref name="organizationId"/> (or no organization).</summary>
    public ApplicationDbContext CreateDbContext(Guid? organizationId) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options,
            new TestCurrentOrganization(organizationId));
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
