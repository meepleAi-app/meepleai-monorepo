using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;

public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddAdministrationContext(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Issue #891: Infrastructure health monitoring service
        services.AddScoped<IInfrastructureHealthService, InfrastructureHealthService>();

        // Issue #893: Prometheus HTTP client with Polly retry policy
        services.AddHttpClient<IPrometheusQueryService, PrometheusHttpClient>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Logging handled by HttpClient logging
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}

