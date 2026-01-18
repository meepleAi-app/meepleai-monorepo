using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;

internal static class AdministrationServiceExtensions
{
#pragma warning disable S1133 // Method marked obsolete but kept for backward compatibility during migration
    [Obsolete("Use AddAdministrationInfrastructure instead for modular registration")]
    public static IServiceCollection AddAdministrationContext(this IServiceCollection services)
#pragma warning restore S1133
    {
        // Repositories
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertConfigurationRepository, AlertConfigurationRepository>();  // Issue #2112: Missing DI registration
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();  // Issue #2112: Missing DI registration
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // ISSUE-916: Reporting repositories
        services.AddScoped<IAdminReportRepository, AdminReportRepository>();
        services.AddScoped<IReportExecutionRepository, ReportExecutionRepository>();

        // Issue #891: Infrastructure health monitoring service
        services.AddScoped<IInfrastructureHealthService, InfrastructureHealthService>();

        // Issue #893: Prometheus HTTP client with Polly retry policy
        services.AddHttpClient<IPrometheusQueryService, PrometheusHttpClient>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Issue #894: Infrastructure details orchestration service
        services.AddScoped<IInfrastructureDetailsService, InfrastructureDetailsService>();

        // Issue #2139: Testing metrics services
        services.AddScoped<IPrometheusClientService, PrometheusClientService>();
        services.AddScoped<ILighthouseReportParserService, LighthouseReportParserService>();
        services.AddScoped<IPlaywrightReportParserService, PlaywrightReportParserService>();

        // ISSUE-2512: Auto-configuration service for first run setup
        services.AddScoped<IAutoConfigurationService, AutoConfigurationService>();

        // Issue #2139: HttpClient for Prometheus queries
        services.AddHttpClient<PrometheusClientService>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // ISSUE-916: Report generation and scheduling services
        services.AddScoped<IReportGeneratorService, ReportGeneratorService>();
        services.AddScoped<IReportSchedulerService, QuartzReportSchedulerService>();

        // ISSUE-916: Quartz.NET configuration for report scheduling
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UseInMemoryStore(); // Use in-memory for alpha; can switch to DB persistence later

            // Register report generation job
            // Issue #2112: Job must be durable if defined without triggers
            q.AddJob<GenerateReportJob>(opts => opts
                .WithIdentity("report-job-template", "reports")
                .StoreDurably(true));  // Must be true - job has no default trigger
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }

    private static AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy()
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

    private static AsyncCircuitBreakerPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
