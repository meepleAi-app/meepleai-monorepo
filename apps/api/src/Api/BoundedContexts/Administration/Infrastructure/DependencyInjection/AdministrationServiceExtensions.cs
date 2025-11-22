using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;

public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddAdministrationContext(this IServiceCollection services)
    {
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
