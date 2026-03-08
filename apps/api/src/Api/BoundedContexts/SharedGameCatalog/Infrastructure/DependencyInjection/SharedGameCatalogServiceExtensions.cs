using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

// Issue #3918: Catalog Trending Analytics Service

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for SharedGameCatalog bounded context.
/// </summary>
internal static class SharedGameCatalogServiceExtensions
{
    /// <summary>
    /// Registers all SharedGameCatalog bounded context services.
    /// Issue #2370 Phase 1
    /// Issue #2454: Background processing services
    /// </summary>
    public static IServiceCollection AddSharedGameCatalogContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Issue #2454: Configure background analysis options
        services.Configure<BackgroundAnalysisOptions>(
            configuration.GetSection(BackgroundAnalysisOptions.SectionName));


        // Register repositories
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<ISharedGameDeleteRequestRepository, SharedGameDeleteRequestRepository>();
        services.AddScoped<ISharedGameDocumentRepository, SharedGameDocumentRepository>(); // Issue #2391 Sprint 1
        services.AddScoped<IGameStateTemplateRepository, GameStateTemplateRepository>(); // Issue #2400 Sprint 3
        services.AddScoped<IRulebookAnalysisRepository, RulebookAnalysisRepository>(); // Issue #2402 Sprint 3
        services.AddScoped<IMechanicDraftRepository, MechanicDraftRepository>(); // Mechanic Extractor: Variant C drafts
        services.AddScoped<IShareRequestRepository, ShareRequestRepository>(); // Issue #2724: CreateShareRequest
        services.AddScoped<IBadgeRepository, BadgeRepository>(); // Issue #2731: Badge gamification system
        services.AddScoped<IUserBadgeRepository, UserBadgeRepository>(); // Issue #2731: User badge awards
        services.AddScoped<IContributorRepository, ContributorRepository>(); // Issue #2735: Contributor stats endpoints
        services.AddScoped<IGameAnalyticsEventRepository, GameAnalyticsEventRepository>(); // Issue #3918: Trending analytics

        // Register domain services
        services.AddScoped<DocumentVersioningService>(); // Issue #2391 Sprint 1
        services.AddScoped<TemplateVersioningService>(); // Issue #2400 Sprint 3
        services.AddScoped<IBadgeEvaluator, BadgeEvaluator>(); // Issue #2728: Badge assignment logic

        // Register application services
        services.AddScoped<IGameStateSchemaGenerator, LlmGameStateSchemaGenerator>(); // Issue #2400 Sprint 3
        services.AddScoped<IQuickQuestionGenerator, LlmQuickQuestionGenerator>(); // Issue #2401 Sprint 3
        services.AddScoped<IRulebookAnalyzer, LlmRulebookAnalyzer>(); // Issue #2402 Sprint 3

        // Issue #2454: Background analysis services
        services.AddScoped<IRulebookOverviewExtractor, LlmRulebookOverviewExtractor>();
        services.AddScoped<ISemanticChunker, EmbeddingBasedSemanticChunker>();
        services.AddScoped<IRulebookChunkAnalyzer, LlmRulebookChunkAnalyzer>();
        services.AddScoped<IRulebookMerger, LlmRulebookMerger>();
        services.AddScoped<IBackgroundRulebookAnalysisOrchestrator, BackgroundRulebookAnalysisOrchestrator>();

        // Issue #2729: Review lock configuration service with caching (Singleton for cache sharing)
        services.AddSingleton<IReviewLockConfigService, ReviewLockConfigService>();

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Issue #2729: Quartz.NET job registration for review lock auto-release
        // Issue #2807: Quartz.NET job registration for top contributor badge assignment
        // Note: Quartz scheduler is configured globally in Administration context.
        // Only register job definition here - do NOT call AddQuartzHostedService (would duplicate).
        services.AddQuartz(q =>
        {
            // Register auto-release expired reviews job
            q.AddJob<AutoReleaseExpiredReviewsJob>(opts => opts
                .WithIdentity("auto-release-expired-reviews-job", "shared-game-catalog")
                .StoreDurably(true));

            // Trigger: Run every 5 minutes
            q.AddTrigger(opts => opts
                .ForJob("auto-release-expired-reviews-job", "shared-game-catalog")
                .WithIdentity("auto-release-expired-reviews-trigger", "shared-game-catalog")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(5)
                    .RepeatForever())
                .WithDescription("Runs every 5 minutes to auto-release expired review locks"));

            // Register top contributor badge job (Issue #2807)
            q.AddJob<TopContributorBadgeJob>(opts => opts
                .WithIdentity("top-contributor-badge-job", "shared-game-catalog")
                .StoreDurably(true));

            // Trigger: Run monthly on the 1st day at midnight UTC
            q.AddTrigger(opts => opts
                .ForJob("top-contributor-badge-job", "shared-game-catalog")
                .WithIdentity("top-contributor-badge-trigger", "shared-game-catalog")
                .WithCronSchedule("0 0 1 * * ?")
                .WithDescription("Runs monthly on the 1st day at midnight UTC to assign TOP_CONTRIBUTOR badge to top 10 contributors"));

            // Issue #3918: Register trending calculation job
            q.AddJob<CalculateTrendingJob>(opts => opts
                .WithIdentity("calculate-trending-job", "shared-game-catalog")
                .StoreDurably(true));

            // Trigger: Run daily at 03:00 UTC
            q.AddTrigger(opts => opts
                .ForJob("calculate-trending-job", "shared-game-catalog")
                .WithIdentity("calculate-trending-trigger", "shared-game-catalog")
                .WithCronSchedule("0 0 3 * * ?")
                .WithDescription("Runs daily at 03:00 UTC to pre-compute trending game scores"));
        });

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }

    /// <summary>
    /// Registers authorization policies for SharedGameCatalog endpoints.
    /// Issue #2371 Phase 2
    /// </summary>
    public static IServiceCollection AddSharedGameCatalogPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOrEditorPolicy", policy =>
                policy.RequireRole("Admin", "Editor"))
            .AddPolicy("AdminOnlyPolicy", policy =>
                policy.RequireRole("Admin"));

        return services;
    }
}
