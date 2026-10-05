using Microsoft.Extensions.DependencyInjection;

namespace ProjectFlow.Infrastructure;

public static class DependencyInjection
{
    // EF Core, PostgreSQL and JWT services are registered here in later milestones.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
