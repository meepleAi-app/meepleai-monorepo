using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.WorkflowIntegration.Infrastructure.DependencyInjection;

public static class WorkflowIntegrationServiceExtensions
{
    public static IServiceCollection AddWorkflowIntegrationContext(this IServiceCollection services)
    {
        services.AddScoped<IN8nConfigurationRepository, N8nConfigurationRepository>();
        services.AddScoped<IWorkflowErrorLogRepository, WorkflowErrorLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
