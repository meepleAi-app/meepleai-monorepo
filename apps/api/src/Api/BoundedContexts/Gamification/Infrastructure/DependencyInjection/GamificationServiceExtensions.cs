using Api.BoundedContexts.Gamification.Application.Jobs;
using Api.BoundedContexts.Gamification.Application.Services;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.BoundedContexts.Gamification.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Api.BoundedContexts.Gamification.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for Gamification bounded context.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal static class GamificationServiceExtensions
{
    /// <summary>
    /// Registers all Gamification bounded context services.
    /// </summary>
    public static IServiceCollection AddGamificationContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IAchievementRepository, AchievementRepository>();
        services.AddScoped<IUserAchievementRepository, UserAchievementRepository>();

        // Register domain services
        services.AddScoped<IAchievementRuleEvaluator, AchievementRuleEvaluator>();

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Quartz.NET job registration
        services.AddQuartz(q =>
        {
            // Issue #3922: Register achievement evaluation job
            q.AddJob<AchievementEvaluationJob>(opts => opts
                .WithIdentity("achievement-evaluation-job", "gamification")
                .StoreDurably(true));

            // Trigger: Run daily at 04:00 UTC
            q.AddTrigger(opts => opts
                .ForJob("achievement-evaluation-job", "gamification")
                .WithIdentity("achievement-evaluation-trigger", "gamification")
                .WithCronSchedule("0 0 4 * * ?")
                .WithDescription("Runs daily at 04:00 UTC to evaluate achievement rules and unlock for all active users"));
        });

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
