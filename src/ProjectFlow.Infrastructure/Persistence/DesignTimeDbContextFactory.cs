using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Infrastructure.Persistence;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools to create migrations without starting the API.
/// No credentials are needed: adding a migration does not connect to the database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=projectflow")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options, new NoCurrentOrganization());
    }

    private sealed class NoCurrentOrganization : ICurrentOrganization
    {
        public Guid? OrganizationId => null;
    }
}
