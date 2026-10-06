using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        services.AddScoped<Tasks.TaskEditor>();

        return services;
    }
}
