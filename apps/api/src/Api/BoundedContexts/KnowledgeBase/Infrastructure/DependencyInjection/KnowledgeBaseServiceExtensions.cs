using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Application.GridSearch.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Reports.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Chunking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>
/// DDD-PHASE3: Dependency injection extensions for KnowledgeBase bounded context.
/// Registers domain services, repositories, adapters, and handlers.
/// </summary>
internal static class KnowledgeBaseServiceExtensions
{
    public static IServiceCollection AddKnowledgeBaseServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        AddDomainServices(services);
        AddValidationServices(services);
        AddLlmServices(services);
        AddInfrastructureServices(services);
        AddApplicationServices(services);
        AddChunkingAndRerankingServices(services, configuration);

        return services;
    }

    private static void AddDomainServices(IServiceCollection services)
    {
        // Domain Services (stateless, can be Singleton for performance)
        services.AddSingleton<VectorSearchDomainService>();
        services.AddSingleton<RrfFusionDomainService>();
        services.AddSingleton<QualityTrackingDomainService>();
        services.AddSingleton<ChatContextDomainService>(); // Issue #857: Chat history context
        services.AddSingleton<AgentOrchestrationService>(); // Issue #867: Agent invocation orchestration
        services.AddSingleton<ChunkingStrategySelector>(); // ISSUE-1903: ADR-016 Phase 1 - Chunking strategy selection

        // Issue #2404: Agent Mode Handlers (Scoped - use repositories and LLM services)
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.PlayerModeHandler>();
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.ChatModeHandler>();

        // Issue #2405: Ledger Mode Handler + State Parser
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.LedgerModeHandler>();
        services.AddScoped<IStateParser, NaturalLanguageStateParser>();
    }

    private static void AddValidationServices(IServiceCollection services)
    {
        // ISSUE-970: BGAI-028 - Confidence Validation (threshold >= 0.70)
        services.AddSingleton<IConfidenceValidationService, ConfidenceValidationService>();

        // ISSUE-971: BGAI-029 - Citation Validation (verify source references)
        services.AddScoped<ICitationValidationService, CitationValidationService>(); // Scoped - uses DbContext

        // ISSUE-972: BGAI-030 - Hallucination Detection (forbidden keywords, multilingual)
        services.AddSingleton<IHallucinationDetectionService, HallucinationDetectionService>();

        // ISSUE-974: BGAI-032 - Multi-Model Consensus Validation (GPT-4 + Claude)
        services.AddSingleton<IMultiModelValidationService, MultiModelValidationService>();

        // ISSUE-981: BGAI-039 - Validation Accuracy Baseline Measurement
        services.AddScoped<ValidationAccuracyTrackingService>();

        // ISSUE-999: BGAI-059 - Golden Dataset Accuracy Testing
        services.AddSingleton<IGoldenDatasetLoader, GoldenDatasetLoader>();
        services.AddSingleton<IRagAccuracyEvaluator, RagAccuracyEvaluator>();

        // ISSUE-977: BGAI-035 - RAG Validation Pipeline (all 5 layers integrated)
        services.AddScoped<IRagValidationPipelineService, RagValidationPipelineService>();
    }

    private static void AddLlmServices(IServiceCollection services)
    {
        // ISSUE-958: LLM Hybrid Architecture
        // Domain Services - Routing Strategy
        services.AddSingleton<ILlmRoutingStrategy, HybridAdaptiveRoutingStrategy>();

        // ISSUE-1725: Model override service for budget-aware downgrading
        services.AddSingleton<ILlmModelOverrideService, LlmModelOverrideService>();

        // ISSUE-1725: Analytics domain services for cost optimization
        // Use Scoped because they depend on scoped repositories (ILlmCostLogRepository)
        services.AddScoped<IQueryEfficiencyAnalyzer, QueryEfficiencyAnalyzer>();
        services.AddSingleton<IModelRecommendationService, ModelRecommendationService>();
        services.AddScoped<ICacheCorrelationAnalyzer, CacheCorrelationAnalyzer>();
        services.AddScoped<IMonthlyOptimizationReportService, MonthlyOptimizationReportService>();

        // ISSUE-960: Cost Tracking (from main)
        // Domain Services - Cost Calculator and Alerting
        services.AddSingleton<ILlmCostCalculator, LlmCostCalculator>();
        services.AddScoped<LlmCostAlertService>(); // Scoped - uses IAlertingService

        // Infrastructure - LLM Clients (Singleton - stateless HTTP clients)
        services.AddSingleton<ILlmClient, OllamaLlmClient>();
        services.AddSingleton<ILlmClient, OpenRouterLlmClient>();

        // ISSUE-2391 Sprint 2: LLM Provider Factory
        services.AddSingleton<LlmProviderFactory>();

        // Application Services - Hybrid LLM Service (Scoped - may use request context)
        services.AddScoped<ILlmService, HybridLlmService>();
        services.AddScoped<HybridLlmService>(sp => (HybridLlmService)sp.GetRequiredService<ILlmService>());

        // ISSUE-962 (BGAI-020): Provider Health Check Service (Singleton - background service)
        services.AddHostedService<ProviderHealthCheckService>();
        services.AddSingleton<IProviderHealthCheckService>(sp =>
            sp.GetServices<IHostedService>().OfType<ProviderHealthCheckService>().ToList()[0]);
        services.AddSingleton<ProviderHealthCheckService>(sp =>
            (ProviderHealthCheckService)sp.GetRequiredService<IProviderHealthCheckService>());

        // ISSUE-1725: LLM budget monitoring background service
        services.AddHostedService<LlmBudgetMonitoringService>();
    }

    private static void AddInfrastructureServices(IServiceCollection services)
    {
        // Infrastructure - Repositories (Scoped - tied to DbContext lifetime)
        services.AddScoped<IVectorDocumentRepository, VectorDocumentRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>(); // Issue #924: ChatThread support
        services.AddScoped<ILlmCostLogRepository, LlmCostLogRepository>(); // ISSUE-960: Cost tracking
        services.AddScoped<IAgentRepository, AgentRepository>(); // Issue #866: Agent management

        // Infrastructure - Adapters (Scoped - uses IQdrantService which is Scoped)
        services.AddScoped<IQdrantVectorStoreAdapter, QdrantVectorStoreAdapter>();
        // Infrastructure - In-Memory Repository (Singleton - shared in-memory store)
        services.AddSingleton<IChunkRepository, InMemoryChunkRepository>();
    }

    private static void AddApplicationServices(IServiceCollection services)
    {
        // Application - Handlers (Scoped - uses Scoped dependencies)
        services.AddScoped<SearchQueryHandler>();
        services.AddScoped<AskQuestionQueryHandler>();

        // ISSUE-2473: Game state parsing for Player Mode AI suggestions
        services.AddScoped<IGameStateParser, GameStateParser>();
        services.AddScoped<GetLlmCostReportQueryHandler>(); // ISSUE-960: Cost reporting
        services.AddScoped<GetQueryEfficiencyReportQueryHandler>(); // ISSUE-1725: Efficiency reporting
        services.AddScoped<GetMonthlyOptimizationReportQueryHandler>(); // ISSUE-1725: Monthly optimization
        services.AddScoped<InvokeAgentCommandHandler>(); // Issue #867: Agent invocation

        services.AddScoped<RunEvaluationCommandHandler>();
        services.AddScoped<LoadDatasetCommandHandler>();
        services.AddScoped<GetEvaluationResultsQueryHandler>();
        services.AddScoped<GetBaselineMetricsQueryHandler>();

        // ISSUE-1907: ADR-016 Phase 5 - Grid Search and Benchmark Reports
        services.AddScoped<RunGridSearchHandler>();
        services.AddSingleton<IReportGeneratorService, ReportGeneratorService>();
    }

    private static void AddChunkingAndRerankingServices(IServiceCollection services, IConfiguration? configuration)
    {
        // ISSUE-1903: ADR-016 Phase 1 - Advanced Chunking
        // Application Service (Scoped - uses ITextChunkingService)
        services.AddScoped<IAdvancedChunkingService, AdvancedChunkingService>();

        // ISSUE-1902: ADR-016 Phase 0 - Dataset Evaluation Service
        // Named IDatasetEvaluationService to avoid conflict with Api.Services.IRagEvaluationService
        services.AddScoped<IDatasetEvaluationService, DatasetEvaluationService>();

        // ISSUE-1906: ADR-016 Phase 4 - Cross-Encoder Reranking Pipeline
        // Domain Services
        services.AddScoped<IParentChunkResolver, ParentChunkResolver>();

        // Infrastructure - HTTP Client for Reranker Service
        services.AddHttpClient<ICrossEncoderReranker, CrossEncoderRerankerClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Reranking:BaseUrl"] ?? "http://localhost:8003";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Options configuration - use provided configuration or defer to runtime resolution
        if (configuration != null)
        {
            services.Configure<RerankerClientOptions>(configuration.GetSection("Reranking"));
            services.Configure<ResilientRetrievalOptions>(configuration.GetSection("ResilientRetrieval"));
        }
        else
        {
            // Fallback: configure with defaults via lambda (resolved at runtime)
            services.AddOptions<RerankerClientOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("Reranking").Bind(opts));
            services.AddOptions<ResilientRetrievalOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("ResilientRetrieval").Bind(opts));
        }

        // Application Services - Resilient Retrieval with Reranking
        services.AddScoped<IRerankedRetrievalService, ResilientRetrievalService>();
    }
}
