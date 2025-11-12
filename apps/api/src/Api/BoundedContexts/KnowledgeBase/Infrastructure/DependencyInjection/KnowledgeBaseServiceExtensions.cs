using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>
/// DDD-PHASE3: Dependency injection extensions for KnowledgeBase bounded context.
/// Registers domain services, repositories, adapters, and handlers.
/// </summary>
public static class KnowledgeBaseServiceExtensions
{
    public static IServiceCollection AddKnowledgeBaseServices(this IServiceCollection services)
    {
        // Domain Services (stateless, can be Singleton for performance)
        services.AddSingleton<VectorSearchDomainService>();
        services.AddSingleton<RrfFusionDomainService>();
        services.AddSingleton<QualityTrackingDomainService>();

<<<<<<< HEAD
        // ISSUE-970: BGAI-028 - Confidence Validation (threshold ≥0.70)
        services.AddSingleton<IConfidenceValidationService, ConfidenceValidationService>();

        // ISSUE-971: BGAI-029 - Citation Validation (verify source references)
        services.AddScoped<ICitationValidationService, CitationValidationService>(); // Scoped - uses DbContext
=======
        // ISSUE-972: BGAI-030 - Hallucination Detection (forbidden keywords, multilingual)
        services.AddSingleton<IHallucinationDetectionService, HallucinationDetectionService>();
>>>>>>> 2ad7549f (feat: Add HallucinationDetectionService with 5-language support (BGAI-030))

        // ISSUE-972: BGAI-030 - Hallucination Detection (forbidden keywords, multilingual)
        services.AddSingleton<IHallucinationDetectionService, HallucinationDetectionService>();

        // ISSUE-958: LLM Hybrid Architecture
        // Domain Services - Routing Strategy
        services.AddSingleton<ILlmRoutingStrategy, HybridAdaptiveRoutingStrategy>();

        // ISSUE-960: Cost Tracking (from main)
        // Domain Services - Cost Calculator and Alerting
        services.AddSingleton<ILlmCostCalculator, LlmCostCalculator>();
        services.AddScoped<LlmCostAlertService>(); // Scoped - uses IAlertingService

        // Infrastructure - LLM Clients (Singleton - stateless HTTP clients)
        services.AddSingleton<ILlmClient, OllamaLlmClient>();
        services.AddSingleton<ILlmClient, OpenRouterLlmClient>();

        // Application Services - Hybrid LLM Service (Scoped - may use request context)
        services.AddScoped<ILlmService, HybridLlmService>();

        // ISSUE-962 (BGAI-020): Provider Health Check Service (Singleton - background service)
        services.AddHostedService<ProviderHealthCheckService>();
        services.AddSingleton<ProviderHealthCheckService>(sp =>
            sp.GetServices<IHostedService>().OfType<ProviderHealthCheckService>().First());

        // Infrastructure - Repositories (Scoped - tied to DbContext lifetime)
        services.AddScoped<IVectorDocumentRepository, VectorDocumentRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>(); // Issue #924: ChatThread support
        services.AddScoped<ILlmCostLogRepository, LlmCostLogRepository>(); // ISSUE-960: Cost tracking

        // Infrastructure - Adapters (Scoped - uses IQdrantService which is Scoped)
        services.AddScoped<IQdrantVectorStoreAdapter, QdrantVectorStoreAdapter>();

        // Application - Handlers (Scoped - uses Scoped dependencies)
        services.AddScoped<SearchQueryHandler>();
        services.AddScoped<AskQuestionQueryHandler>();
        services.AddScoped<GetLlmCostReportQueryHandler>(); // ISSUE-960: Cost reporting

        return services;
    }
}
