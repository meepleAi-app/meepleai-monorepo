using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IShareRequestRepository, ShareRequestRepository>(); // Issue #2724: CreateShareRequest

        // Register domain services
        services.AddScoped<DocumentVersioningService>(); // Issue #2391 Sprint 1
        services.AddScoped<TemplateVersioningService>(); // Issue #2400 Sprint 3

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

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

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
                policy.RequireRole("admin", "editor"))
            .AddPolicy("AdminOnlyPolicy", policy =>
                policy.RequireRole("admin"));

        return services;
    }
}
