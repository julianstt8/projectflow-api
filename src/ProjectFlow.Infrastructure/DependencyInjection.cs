using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Infrastructure.Authentication;
using ProjectFlow.Infrastructure.Persistence;
using ProjectFlow.Infrastructure.Persistence.Seeding;

namespace ProjectFlow.Infrastructure;

public static class DependencyInjection
{
    // JWT services are registered here in milestone 3.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string is read lazily, when a DbContext is first needed,
        // so the API can start (e.g. health checks, tests) without a database configured.
        services.AddDbContext<ApplicationDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString(PersistenceConstants.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{PersistenceConstants.ConnectionStringName}' is not configured."))
            .UseSnakeCaseNamingConvention());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DevelopmentDataSeeder>();

        return services;
    }
}
