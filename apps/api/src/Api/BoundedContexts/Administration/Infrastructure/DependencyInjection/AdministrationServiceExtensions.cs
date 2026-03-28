using Api.BoundedContexts.Administration.Application.Configuration;
using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Configuration;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Core;
using Api.Infrastructure.Seeders.LivedIn;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;

internal static class AdministrationServiceExtensions
{
    public static IServiceCollection AddAdministrationContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repositories
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertConfigurationRepository, AlertConfigurationRepository>();  // Issue #2112: Missing DI registration
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();  // Issue #2112: Missing DI registration
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // ISSUE-916: Reporting repositories
        services.AddScoped<IAdminReportRepository, AdminReportRepository>();
        services.AddScoped<IReportExecutionRepository, ReportExecutionRepository>();

        // Issue #3464: RAG pipeline strategy repository
        services.AddScoped<IRagPipelineStrategyRepository, RagPipelineStrategyRepository>();

        // Issue #4459: RAG execution replay repository
        services.AddScoped<IRagExecutionRepository, RagExecutionRepository>();

        // Issue #5512: GDPR AI consent tracking
        services.AddScoped<IUserAiConsentRepository, UserAiConsentRepository>();

        // Issue #3692: Token Management repositories + OpenRouter API
        services.AddScoped<ITokenTierRepository, TokenTierRepository>();
        services.AddScoped<IUserTokenUsageRepository, UserTokenUsageRepository>();
        services.AddScoped<ITokenTrackingService, TokenTrackingService>(); // Issue #3786: Token tracking service
        services.AddHttpClient<IOpenRouterService, OpenRouterService>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Budget Display System: Credit-based budget services
        services.AddScoped<ICreditConversionService, CreditConversionService>();
        services.AddScoped<IUserBudgetService, UserBudgetService>();
        services.AddScoped<IAdminBudgetService, AdminBudgetService>();

        // Issue #3693: Batch Job System repositories
        services.AddScoped<IBatchJobRepository, BatchJobRepository>();

        // RAG Backup services (Task 10: export, import, snapshot storage)
        services.AddScoped<IRagBackupStorageService, RagBackupStorageService>();
        services.AddScoped<IRagExportService, RagExportService>();

        // MVP Stack Optimization: Vector re-embedding service (e5-large → mxbai-embed-large migration)
        services.AddScoped<VectorReembeddingService>();

        // ISSUE-2528: Orphaned task cleanup configuration and service
        services.Configure<OrphanedTaskCleanupOptions>(
            configuration.GetSection(OrphanedTaskCleanupOptions.SectionKey));
        services.AddScoped<IOrphanedTaskCleanupService, OrphanedTaskCleanupService>();

        // Issue #138: Docker Socket Proxy service for admin container management
        services.AddHttpClient<IDockerProxyService, DockerProxyService>(client =>
        {
            var host = Environment.GetEnvironmentVariable("DOCKER_PROXY_HOST") ?? "docker-socket-proxy";
            var port = Environment.GetEnvironmentVariable("DOCKER_PROXY_PORT") ?? "2375";
            client.BaseAddress = new Uri($"http://{host}:{port}");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Issue #891: Infrastructure health monitoring service
        services.AddScoped<IInfrastructureHealthService, InfrastructureHealthService>();

        // Issue #448: Service health monitoring background service with hysteresis
        services.Configure<HealthMonitorOptions>(configuration.GetSection(HealthMonitorOptions.SectionName));
        services.AddHostedService<InfrastructureHealthMonitorService>();

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

        // ISSUE-2512: AutoConfigurationService removed — replaced by SeedOrchestrator (Epic #318)

        // Epic #318: Layered seeding system
        services.AddScoped<ISeedLayer, CoreSeedLayer>();
        services.AddScoped<ISeedLayer, CatalogSeedLayer>();
        services.AddScoped<ISeedLayer, LivedInSeedLayer>();
        services.AddScoped<SeedOrchestrator>();

        // Issue #3916: AI insights service for personalized dashboard recommendations
        services.AddScoped<IAiInsightsService, AiInsightsService>();

        // Issue #4308: Domain analyzers for AI insights (RAG upgrade)
        services.AddScoped<IUserInsightsService, UserInsightsService>();
        services.AddScoped<IBacklogAnalyzer, BacklogAnalyzer>();
        services.AddScoped<IRulesAnalyzer, RulesAnalyzer>();
        services.AddScoped<IRAGRecommender, RAGRecommender>();
        services.AddScoped<IStreakAnalyzer, StreakAnalyzer>();

        // Issue #3324: SSE real-time dashboard streaming service
        // Singleton because it holds Channel-based subscriber state across requests
        services.AddSingleton<IDashboardStreamService, DashboardStreamService>();

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
#pragma warning disable CS0618 // UseMicrosoftDependencyInjectionJobFactory is obsolete but still functional
            q.UseMicrosoftDependencyInjectionJobFactory();
#pragma warning restore CS0618
            q.UseInMemoryStore(); // Use in-memory for alpha; can switch to DB persistence later

            // Register report generation job
            // Issue #2112: Job must be durable if defined without triggers
            q.AddJob<GenerateReportJob>(opts => opts
                .WithIdentity("report-job-template", "reports")
                .StoreDurably(true));  // Must be true - job has no default trigger

            // ISSUE-2528: Register orphaned task cleanup job with hourly cron trigger
            q.AddJob<OrphanedTaskCleanupJob>(opts => opts
                .WithIdentity("orphaned-task-cleanup-job", "maintenance")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("orphaned-task-cleanup-job", "maintenance")
                .WithIdentity("orphaned-task-cleanup-trigger", "maintenance")
                .WithCronSchedule("0 0 * * * ?")  // Every hour at minute 0
                .WithDescription("Runs hourly to clean up orphaned analysis tasks older than retention period"));

            // Issue #3691: Audit log retention cleanup job (daily at 3 AM UTC)
            q.AddJob<AuditLogRetentionJob>(opts => opts
                .WithIdentity("audit-log-retention-job", "maintenance")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("audit-log-retention-job", "maintenance")
                .WithIdentity("audit-log-retention-trigger", "maintenance")
                .WithCronSchedule("0 0 3 * * ?")  // Daily at 3:00 AM UTC
                .WithDescription("Runs daily to clean up audit logs older than 90 days"));

            // Issue #3693 Task 2: Batch job processor (every 30 seconds)
            q.AddJob<BatchJobProcessorJob>(opts => opts
                .WithIdentity("batch-job-processor", "background")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("batch-job-processor", "background")
                .WithIdentity("batch-job-processor-trigger", "background")
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever())
                .WithDescription("Processes queued batch jobs every 30 seconds"));
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
