using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectFlow.Infrastructure.Persistence.Seeding;

namespace ProjectFlow.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>Applies pending EF Core migrations. Used on startup when <c>Database:MigrateOnStartup</c> is set (local development, public demo).</summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Inserts the fake demo data if it is not there yet. Used on startup when <c>Database:SeedOnStartup</c> is set.</summary>
    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();

        await seeder.SeedAsync(cancellationToken);
    }
}
