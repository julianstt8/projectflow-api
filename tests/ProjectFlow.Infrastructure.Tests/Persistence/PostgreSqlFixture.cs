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
    public ApplicationDbContext CreateDbContext(Guid? organizationId) => CreateDbContext(_container.GetConnectionString(), organizationId);

    /// <summary>
    /// Creates and migrates a separate database in the same container, for tests that empty whole tables
    /// and must not touch the data of the other tests. Returns its connection string.
    /// </summary>
    public async Task<string> CreateIsolatedDatabaseAsync()
    {
        var name = $"isolated_{Guid.NewGuid():N}";
        await using (var connection = new Npgsql.NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();

            // CREATE DATABASE takes no parameters; the name is generated above, never input.
            await using var command = new Npgsql.NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
        await using var isolated = CreateDbContext(connectionString, organizationId: null);
        await isolated.Database.MigrateAsync();
        return connectionString;
    }

    public static ApplicationDbContext CreateDbContext(string connectionString, Guid? organizationId) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options,
            new TestCurrentOrganization(organizationId));
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
