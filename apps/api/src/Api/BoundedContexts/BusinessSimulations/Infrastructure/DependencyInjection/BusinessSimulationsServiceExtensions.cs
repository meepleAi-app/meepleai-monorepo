using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Application.Jobs;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.DependencyInjection;

/// <summary>
/// DI registration for BusinessSimulations bounded context.
/// Issue #3720: Financial Ledger Data Model (Epic #3688)
/// Issue #3721: Automatic Ledger Tracking (Epic #3688)
/// Issue #3724: Export Ledger - Scheduled Reports (Epic #3688)
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal static class BusinessSimulationsServiceExtensions
{
    public static IServiceCollection AddBusinessSimulationsContext(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<ICostScenarioRepository, CostScenarioRepository>();

        // Services (Issue #3721)
        services.AddScoped<ILedgerTrackingService, LedgerTrackingService>();

        // Quartz.NET job registration (Issue #3721)
        services.AddQuartz(q =>
        {
            // Infrastructure cost tracking job - daily at 05:00 UTC
            q.AddJob<InfrastructureCostTrackingJob>(opts => opts
                .WithIdentity("infrastructure-cost-tracking-job", "business-simulations")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("infrastructure-cost-tracking-job", "business-simulations")
                .WithIdentity("infrastructure-cost-tracking-trigger", "business-simulations")
                .WithCronSchedule("0 0 5 * * ?")
                .WithDescription("Runs daily at 05:00 UTC to aggregate previous day LLM API costs into financial ledger"));

            // Monthly ledger report job - 1st of each month at 06:00 UTC (Issue #3724)
            q.AddJob<MonthlyLedgerReportJob>(opts => opts
                .WithIdentity("monthly-ledger-report-job", "business-simulations")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("monthly-ledger-report-job", "business-simulations")
                .WithIdentity("monthly-ledger-report-trigger", "business-simulations")
                .WithCronSchedule("0 0 6 1 * ?")
                .WithDescription("Runs on the 1st of each month at 06:00 UTC to generate previous month PDF report"));
        });

        // MediatR handlers (TokenUsageLedgerEventHandler) are auto-registered via assembly scanning

        return services;
    }
}
