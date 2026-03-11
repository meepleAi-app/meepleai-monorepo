using Api.BoundedContexts.WorkflowIntegration.Application.Services;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.WorkflowIntegration.Infrastructure.DependencyInjection;

internal static class WorkflowIntegrationServiceExtensions
{
    public static IServiceCollection AddWorkflowIntegrationContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IN8NConfigurationRepository, N8NConfigurationRepository>();
        services.AddScoped<IWorkflowErrorLogRepository, WorkflowErrorLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Issue #57-#58: n8n webhook client for API → n8n triggers
        services.Configure<N8nWebhookClientOptions>(configuration.GetSection(N8nWebhookClientOptions.SectionName));
        services.AddSingleton<IN8nWebhookClient, N8nWebhookClient>();

        return services;
    }
}
