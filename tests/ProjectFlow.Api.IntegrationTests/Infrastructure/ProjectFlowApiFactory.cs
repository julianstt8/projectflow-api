using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ProjectFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL in Docker. The API applies the migrations on
/// startup, exactly as with docker-compose; demo data is not seeded so tests control their own data.
/// </summary>
public sealed class ProjectFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Starts the host, which applies the migrations.
        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>A context on the test database working inside <paramref name="organizationId"/> (or no organization).</summary>
    public ApplicationDbContext CreateDbContext(Guid? organizationId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options, new FixedCurrentOrganization(organizationId));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "false");
    }

    private sealed class FixedCurrentOrganization(Guid? organizationId) : ICurrentOrganization
    {
        public Guid? OrganizationId { get; } = organizationId;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ProjectFlowApiFactory>
{
    public const string Name = "API with PostgreSQL";
}
