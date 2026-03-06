using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
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
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
// Note: ITierStrategyAccessService is in Api.BoundedContexts.KnowledgeBase.Domain.Services namespace
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Chunking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Infrastructure.Persistence;
using Quartz;

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
        AddCachingServices(services, configuration);
        AddBackgroundJobServices(services, configuration);

        return services;
    }

    private static void AddDomainServices(IServiceCollection services)
    {
        // Domain Services (stateless, can be Singleton for performance)
        services.AddSingleton<VectorSearchDomainService>();
        services.AddSingleton<RrfFusionDomainService>();
        services.AddSingleton<QualityTrackingDomainService>();
        services.AddSingleton<ChatContextDomainService>(); // Issue #857: Chat history context
        services.AddScoped<IConversationQueryRewriter, Application.Services.ConversationQueryRewriter>(); // Issue #5258: Query rewriting for multi-turn RAG
        services.AddScoped<IConversationSummarizer, Application.Services.ConversationSummarizer>(); // Issue #5259: Progressive conversation summarization
        services.AddSingleton<AgentOrchestrationService>(); // Issue #867: Agent invocation orchestration
        services.AddSingleton<ChunkingStrategySelector>(); // ISSUE-1903: ADR-016 Phase 1 - Chunking strategy selection
        services.AddSingleton<IAgentPromptBuilder, AgentPromptBuilder>(); // Issue #3184 (AGT-010): Session agent prompt building
        services.AddSingleton<IModelConfigurationService, ModelConfigurationService>(); // Issue #3377: Models tier endpoint
        // Issue #3436: Tier-Strategy Access Validation Service
        // Scoped - uses ITierStrategyAccessRepository which depends on DbContext
        services.AddScoped<ITierStrategyAccessService, TierStrategyAccessService>();

        // Issue #2404: Agent Mode Handlers (Scoped - use repositories and LLM services)
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.PlayerModeHandler>();
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.ChatModeHandler>();

        // Issue #2405: Ledger Mode Handler + State Parser
        services.AddScoped<IAgentModeHandler, Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes.LedgerModeHandler>();
        services.AddScoped<IStateParser, NaturalLanguageStateParser>();

        // ISSUE-3491: Context Engineering Framework
        services.AddSingleton<ITokenEstimator, SimpleTokenEstimator>();
        services.AddSingleton<IContextRetrievalStrategy, TemporalScoringStrategy>();
        services.AddSingleton<IContextRetrievalStrategy, PositionSimilarityStrategy>();
        services.AddSingleton<IContextRetrievalStrategy, HybridSearchStrategy>();

        // Issue #3772: Game State Parser for Decisore Agent
        services.AddSingleton<IGameStateParserService, GameStateParserFactory>();

        // Issue #3770: Move Generator for Decisore Agent
        services.AddScoped<IMoveGeneratorService, ChessMoveGenerator>();
        services.AddScoped<ILegalMoveValidator, LegalMoveValidator>();
        services.AddScoped<IMoveScorer, HeuristicMoveScorer>();

        // Issue #3769: Decisore Agent Strategic Analysis
        services.AddScoped<IDecisoreAgentService, DecisoreAgentService>();

        // Issue #3771: Multi-Model Ensemble Evaluation
        services.AddScoped<IMultiModelEvaluator, MultiModelEvaluator>();
        services.AddSingleton<IContextRetrievalStrategy, CapabilityMatchingStrategy>();
        services.AddScoped<IContextSource, ConversationMemorySource>();
        services.AddScoped<GameStateSource>();
        services.AddScoped<IContextSource, GameStateSource>(sp => sp.GetRequiredService<GameStateSource>());
        services.AddScoped<IContextSource, StrategyPatternSource>();

        // ISSUE-3492: Hybrid Search with Reranking Pipeline
        services.AddScoped<IHybridSearchEngine, HybridSearchEngine>();

        // ISSUE-3491: Context Assembler (orchestrates multi-source context assembly)
        services.AddScoped<ContextAssembler>();

        // ISSUE-3760: Arbitro Agent Service (AI-powered move validation)
        services.AddScoped<IArbitroAgentService, ArbitroAgentService>();

        // ISSUE-4336: Multi-Agent Router (intelligent routing between Tutor/Arbitro/Decisore)
        services.AddSingleton<Domain.Services.MultiAgentRouter.IntentClassifier>();
        services.AddSingleton<Domain.Services.MultiAgentRouter.RoutingMetricsCollector>();
        services.AddSingleton<Domain.Services.MultiAgentRouter.AgentRouterService>();

        // ISSUE-4337: Agent State Coordination (shared context across agents)
        services.AddScoped<AgentStateCoordinator>();
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

        // Issue #3435: Strategy-Model mapping service (Singleton - uses IServiceScopeFactory for DB access)
        services.AddSingleton<IStrategyModelMappingService, StrategyModelMappingService>();

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
        // ISSUE-5086: IServiceScopeFactory is auto-injected by the DI container (built-in singleton)
        // enabling fire-and-forget circuit breaker notifications without coupling to request scope
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

        // Issue #5073: Rotating JSONL file logger for OpenRouter requests (Singleton - owns file handle)
        services.AddSingleton<IOpenRouterFileLogger>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logDir = config["OPENROUTER_LOG_PATH"] ?? Path.Combine("logs", "openrouter");
            Directory.CreateDirectory(logDir);
            return new OpenRouterFileLogger(logDir);
        });

        // Issue #5074: OpenRouter usage cache — polls /auth/key every 60s, accumulates daily spend
        // Register as concrete singleton first so IOpenRouterUsageService can be resolved directly
        // without going through GetServices<IHostedService>() which causes a circular DI deadlock
        // (OpenRouterRpmAlertBackgroundService → IOpenRouterRateLimitTracker → IOpenRouterUsageService
        //  → GetServices<IHostedService>() → OpenRouterRpmAlertBackgroundService → deadlock).
        services.AddSingleton<OpenRouterUsageService>();
        services.AddHostedService(sp => sp.GetRequiredService<OpenRouterUsageService>());
        services.AddSingleton<IOpenRouterUsageService>(sp => sp.GetRequiredService<OpenRouterUsageService>());

        // Issue #5075: RPM/TPM rate limit tracker using Redis sliding window (Singleton - stateless HTTP-independent)
        services.AddSingleton<IOpenRouterRateLimitTracker, OpenRouterRateLimitTracker>();

        // ISSUE-5084: Background service that polls OpenRouter RPM utilization and alerts admins
        services.AddHostedService<OpenRouterRpmAlertBackgroundService>();

        // ISSUE-5085: Background service that monitors daily OpenRouter budget and alerts admins at thresholds
        services.AddHostedService<OpenRouterBudgetAlertBackgroundService>();

        // Issue #5087: Free model quota tracker — Redis-backed RPD exhaustion state (Singleton - stateless)
        services.AddSingleton<IFreeModelQuotaTracker, FreeModelQuotaTracker>();
    }

    private static void AddInfrastructureServices(IServiceCollection services)
    {
        // Infrastructure - Unit of Work (Scoped - tied to DbContext lifetime)
        // Issue #3177: AGT-003 - Required by AgentTypology command handlers
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Infrastructure - Repositories (Scoped - tied to DbContext lifetime)
        services.AddScoped<IVectorDocumentRepository, VectorDocumentRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>(); // Issue #924: ChatThread support
        services.AddScoped<ILlmCostLogRepository, LlmCostLogRepository>(); // ISSUE-960: Cost tracking
        services.AddScoped<ILlmRequestLogRepository, LlmRequestLogRepository>(); // ISSUE-5072: Detailed request logging with 30-day retention
        services.AddScoped<IAgentRepository, AgentRepository>(); // Issue #866: Agent management
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>(); // Issue #3808: AgentDefinition for AI Lab
        services.AddScoped<IAgentTypologyRepository, AgentTypologyRepository>(); // Issue #3175, #3177: AgentTypology CRUD
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>(); // Issue #3184 (AGT-010): Agent session lifecycle
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>(); // Issue #3483: Chat session persistence
        services.AddScoped<IAgentTestResultRepository, AgentTestResultRepository>(); // Issue #3379: Agent test results
        services.AddScoped<IArbitroValidationFeedbackRepository, ArbitroValidationFeedbackRepository>(); // Issue #4328: Arbitro beta testing feedback
        services.AddScoped<IDecisoreMoveFeedbackRepository, DecisoreMoveFeedbackRepository>(); // Issue #4335: Decisore beta testing feedback
        services.AddScoped<ITierStrategyAccessRepository, TierStrategyAccessRepository>(); // Issue #3436: Tier-Strategy access
        services.AddScoped<IStrategyModelMappingRepository, StrategyModelMappingRepository>(); // Issue #3435: Strategy-Model mapping
        services.AddScoped<ICustomRagPipelineRepository, CustomRagPipelineRepository>(); // Issue #3120: Custom RAG pipeline management
        // Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System
        services.AddScoped<IConversationMemoryRepository, ConversationMemoryRepository>();
        services.AddScoped<IAgentGameStateSnapshotRepository, AgentGameStateSnapshotRepository>();
        services.AddScoped<IStrategyPatternRepository, StrategyPatternRepository>();
        services.AddScoped<IPlaygroundTestScenarioRepository, PlaygroundTestScenarioRepository>(); // Issue #4396: Playground test scenarios
        services.AddScoped<IRagExecutionRepository, RagExecutionRepository>(); // Issue #4458: RAG execution history
        services.AddScoped<IRagUserConfigRepository, RagUserConfigRepository>(); // Issue #5311: Per-user RAG config persistence

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

    private static void AddCachingServices(IServiceCollection services, IConfiguration? configuration)
    {
        // ISSUE-3494: Multi-Tier Cache Configuration
        if (configuration != null)
        {
            services.Configure<MultiTierCacheConfiguration>(
                configuration.GetSection(MultiTierCacheConfiguration.SectionName));
        }
        else
        {
            // Fallback: configure with defaults via lambda (resolved at runtime)
            services.AddOptions<MultiTierCacheConfiguration>()
                .Configure<IConfiguration>((opts, cfg) =>
                    cfg.GetSection(MultiTierCacheConfiguration.SectionName).Bind(opts));
        }

        // ISSUE-3494: Multi-Tier Cache Service (Singleton for L1 in-memory cache consistency)
        services.AddSingleton<IMultiTierCache, MultiTierCache>();
    }

    private static void AddBackgroundJobServices(IServiceCollection services, IConfiguration? configuration)
    {
        // Issue #3498: Conversation Memory Cleanup Configuration
        if (configuration != null)
        {
            services.Configure<ConversationMemoryCleanupOptions>(
                configuration.GetSection(ConversationMemoryCleanupOptions.SectionKey));
        }
        else
        {
            // Fallback: configure with defaults via lambda (resolved at runtime)
            services.AddOptions<ConversationMemoryCleanupOptions>()
                .Configure<IConfiguration>((opts, cfg) =>
                    cfg.GetSection(ConversationMemoryCleanupOptions.SectionKey).Bind(opts));
        }

        // Issue #3498: Quartz.NET job registration for GDPR-compliant conversation memory cleanup
        // Note: Quartz scheduler is configured globally in Administration context.
        // Only register job definition here - do NOT call AddQuartzHostedService (would duplicate).
        services.AddQuartz(q =>
        {
            // Register conversation memory cleanup job
            q.AddJob<ConversationMemoryCleanupJob>(opts => opts
                .WithIdentity("conversation-memory-cleanup-job", "knowledge-base")
                .StoreDurably(true));

            // Trigger: Run daily at 3:00 AM UTC (low-traffic period)
            q.AddTrigger(opts => opts
                .ForJob("conversation-memory-cleanup-job", "knowledge-base")
                .WithIdentity("conversation-memory-cleanup-trigger", "knowledge-base")
                .WithCronSchedule("0 0 3 * * ?")
                .WithDescription("Runs daily at 3 AM UTC to clean up conversation memories older than 90 days for GDPR compliance"));
        });

        // Issue #5072: Daily cleanup of expired LLM request logs (30-day retention)
        // Only register job definition here - do NOT call AddQuartzHostedService (would duplicate).
        services.AddQuartz(q =>
        {
            q.AddJob<LlmRequestLogCleanupJob>(opts => opts
                .WithIdentity("llm-request-log-cleanup-job", "knowledge-base")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("llm-request-log-cleanup-job", "knowledge-base")
                .WithIdentity("llm-request-log-cleanup-trigger", "knowledge-base")
                .WithCronSchedule("0 0 4 * * ?")
                .WithDescription("Runs daily at 4 AM UTC to delete LLM request logs older than 30 days"));
        });

        // ISSUE-5085: Daily OpenRouter usage digest sent to all admins at 08:00 UTC
        services.AddQuartz(q =>
        {
            q.AddJob<OpenRouterDailySummaryJob>(opts => opts
                .WithIdentity("openrouter-daily-summary-job", "knowledge-base")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("openrouter-daily-summary-job", "knowledge-base")
                .WithIdentity("openrouter-daily-summary-trigger", "knowledge-base")
                .WithCronSchedule("0 0 8 * * ?")
                .WithDescription("Runs daily at 8 AM UTC to send yesterday's OpenRouter usage digest to all admins"));
        });
    }
}
