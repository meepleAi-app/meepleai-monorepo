using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;

public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddAdministrationContext(this IServiceCollection services)
    {
        // Repository implementations pending
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
