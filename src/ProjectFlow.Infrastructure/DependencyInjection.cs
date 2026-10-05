using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Infrastructure.Authentication;
using ProjectFlow.Infrastructure.Persistence;
using ProjectFlow.Infrastructure.Persistence.Repositories;
using ProjectFlow.Infrastructure.Persistence.Seeding;

namespace ProjectFlow.Infrastructure;

public static class DependencyInjection
{
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DevelopmentDataSeeder>();

        // Validated at startup: the API refuses to start with a missing or too short signing key.
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        return services;
    }
}
