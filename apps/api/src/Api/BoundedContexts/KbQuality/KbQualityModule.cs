using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.BoundedContexts.KbQuality.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KbQuality;

/// <summary>
/// DI registration entry point for the KbQuality bounded context (#1675).
/// Wires aggregate repo, application services, MediatR behaviors, ports/adapters,
/// background jobs, and configuration options.
/// </summary>
public static class KbQualityModule
{
    public static IServiceCollection AddKbQualityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EvalQualityOptions>(configuration.GetSection(EvalQualityOptions.SectionName));

        services.AddSingleton<IEvaluationMetricsCalculator, EvaluationMetricsCalculator>();
        services.AddSingleton<IQualityBandResolver, QualityBandResolver>();
        services.AddScoped<IEvaluationCostEstimator, EvaluationCostEstimator>();
        services.AddScoped<IGoldsetGenerator, LlmGoldsetGenerator>();
        services.AddScoped<IEvaluationExecutor, EvaluationExecutor>();

        // EvaluationRepository implements 3 ports — single scoped registration backs all three.
        services.AddScoped<EvaluationRepository>();
        services.AddScoped<IEvaluationRepository>(sp => sp.GetRequiredService<EvaluationRepository>());
        services.AddScoped<IEvaluationRateLimitStore>(sp => sp.GetRequiredService<EvaluationRepository>());
        services.AddScoped<IEvalCostBudgetChecker>(sp => sp.GetRequiredService<EvaluationRepository>());

        return services;
    }
}
