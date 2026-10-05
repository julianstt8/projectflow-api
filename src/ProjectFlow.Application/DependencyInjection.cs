using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        return services;
    }
}
