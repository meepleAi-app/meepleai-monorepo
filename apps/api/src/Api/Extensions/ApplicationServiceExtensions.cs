using Api.BoundedContexts.Authentication.Infrastructure.DependencyInjection;
using Api.BoundedContexts.GameManagement.Infrastructure.DependencyInjection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.DependencyInjection;
using Api.Helpers;
using Api.Services;
using Api.Services.Chat;
using Api.Services.Pdf;
using Api.Services.Qdrant;
using Api.Services.Rag;
using Api.Observability;

namespace Api.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddVectorSearchServices();
        services.AddDomainServices();
        services.AddAiServices();
        services.AddPdfServices();
        services.AddChatServices();
        services.AddAdminServices();
        services.AddChessServices();
        services.AddBggServices();
        services.AddQualityServices();

        // DDD-PHASE2: Authentication bounded context (repositories for CQRS handlers)
        services.AddAuthenticationContext();

        // DDD-PHASE2: GameManagement bounded context (repositories for CQRS handlers)
        services.AddGameManagementContext();

        // DDD-PHASE3: KnowledgeBase bounded context
        services.AddKnowledgeBaseServices();

        // DDD-PHASE3: WorkflowIntegration bounded context
        services.AddWorkflowIntegrationContext();

        // DDD-PHASE3: SystemConfiguration bounded context
        services.AddSystemConfigurationContext();

        return services;
    }

    private static IServiceCollection AddVectorSearchServices(this IServiceCollection services)
    {
        // AI-01: Vector search services
        services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();

        // Qdrant specialized services (SOLID refactoring)
        services.AddScoped<IQdrantCollectionManager, QdrantCollectionManager>();
        services.AddScoped<IQdrantVectorIndexer, QdrantVectorIndexer>();
        services.AddScoped<IQdrantVectorSearcher, QdrantVectorSearcher>();

        // Qdrant facade service (Scoped to match specialized services lifetime)
        services.AddScoped<IQdrantService, QdrantService>();

        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<PdfIndexingService>();

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Game and RuleSpec services
        services.AddScoped<GameService>();
        services.AddScoped<RuleSpecService>();
        services.AddScoped<RuleSpecDiffService>();
        services.AddScoped<RuleCommentService>(); // EDIT-05: Comment service with threading and mentions
        services.AddScoped<RuleSpecCommentService>(); // EDIT-02: Legacy comment service

        // CONFIG-01: Dynamic configuration service
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // CONFIG: Configuration wrapper (TEST-900 RC-1: enables mocking of IConfiguration extension methods)
        services.AddSingleton<IConfigurationWrapper>(sp =>
            new ConfigurationWrapper(sp.GetRequiredService<IConfiguration>()));

        // CONFIG: Configuration helper for 3-tier fallback (Database → Config → Defaults)
        services.AddScoped<ConfigurationHelper>();

        // CONFIG-05: Feature flags service
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // N8N services
        services.AddScoped<N8nConfigService>();
        services.AddScoped<N8nTemplateService>(); // N8N-04: Workflow template service

        // N8N-05: Workflow error logging service
        services.AddScoped<IWorkflowErrorLoggingService, WorkflowErrorLoggingService>();

        return services;
    }

    private static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        // AI-15-ALT: Use LlmService (OpenRouter with GPT-4o-mini model selection) instead of OllamaLlmService
        services.AddScoped<ILlmService, LlmService>();

        // AI-09: Language detection for multi-language support
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();

        // AI-05: AI response caching (PERF-03: Changed from Singleton to Scoped due to MeepleAiDbContext dependency)
        services.AddScoped<IAiResponseCacheService, AiResponseCacheService>();

        // SOLID Phase 3: RAG sub-services (specialized services extracted from RagService)
        services.AddScoped<IQueryExpansionService, QueryExpansionService>();
        services.AddScoped<ISearchResultReranker, SearchResultReranker>();
        services.AddScoped<ICitationExtractorService, CitationExtractorService>();

        // RAG and search services
        services.AddScoped<IRagService, RagService>(); // AI-04: RAG service (now a facade over specialized sub-services)
        services.AddScoped<IKeywordSearchService, KeywordSearchService>(); // AI-14: PostgreSQL full-text keyword search
        services.AddScoped<IHybridSearchService, HybridSearchService>(); // AI-14: Hybrid search with RRF fusion

        // Streaming services
        services.AddScoped<IStreamingRagService, StreamingRagService>(); // API-02: Streaming RAG service
        services.AddScoped<IStreamingQaService, StreamingQaService>(); // CHAT-01: Streaming QA service

        // Setup and prompt services
        services.AddScoped<SetupGuideService>();
        services.AddScoped<IPromptTemplateService, PromptTemplateService>(); // AI-07.1: Prompt template service
        services.AddScoped<IPromptEvaluationService, PromptEvaluationService>(); // ADMIN-01 Phase 4: Prompt evaluation

        // AI-06: RAG offline evaluation service
        services.AddScoped<IRagEvaluationService, RagEvaluationService>();

        // AI-07: Prompt versioning and management service
        services.AddScoped<IPromptManagementService, PromptManagementService>();

        // Audit and logging
        services.AddScoped<AuditService>();
        services.AddScoped<AiRequestLogService>();
        services.AddScoped<AgentFeedbackService>();

        return services;
    }

    private static IServiceCollection AddPdfServices(this IServiceCollection services)
    {
        // PDF sub-services (SOLID refactoring - Phase 3)
        services.AddScoped<ITableDetectionService, TableDetectionService>();
        services.AddScoped<ITableCellParser, TableCellParser>();
        services.AddScoped<ITableStructureAnalyzer, TableStructureAnalyzer>();
        services.AddScoped<IPdfMetadataExtractor, PdfMetadataExtractor>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();

        // PDF main services
        services.AddScoped<PdfTextExtractionService>();
        services.AddScoped<PdfTableExtractionService>(); // Refactored facade
        services.AddScoped<IPdfValidationService, PdfValidationService>(); // PDF-09: PDF validation service
        services.AddScoped<PdfStorageService>(); // Refactored facade

        // PDF-02: OCR service for fallback text extraction (Windows-only)
#pragma warning disable CA1416 // TesseractOcrService is Windows-only by design
        services.AddSingleton<IOcrService, TesseractOcrService>();
#pragma warning restore CA1416

        return services;
    }

    private static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();
        services.AddScoped<IFollowUpQuestionService, FollowUpQuestionService>(); // CHAT-02: Follow-up question generation

        // CHAT-05: Chat export services
        services.AddScoped<IChatExportService, ChatExportService>();
        services.AddScoped<IExportFormatter, TxtExportFormatter>();
        services.AddScoped<IExportFormatter, MdExportFormatter>();
        services.AddScoped<IExportFormatter, PdfExportFormatter>();

        return services;
    }

    private static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        // ADMIN-01: User management service
        services.AddScoped<UserManagementService>();

        // ADMIN-02: Analytics dashboard service
        services.AddScoped<IAdminStatsService, AdminStatsService>();

        return services;
    }

    private static IServiceCollection AddChessServices(this IServiceCollection services)
    {
        // CHESS-03: Chess knowledge indexing service
        services.AddScoped<IChessKnowledgeService, ChessKnowledgeService>();

        // CHESS-04: Chess conversational agent service
        services.AddScoped<IChessAgentService, ChessAgentService>();

        return services;
    }

    private static IServiceCollection AddBggServices(this IServiceCollection services)
    {
        // AI-13: BoardGameGeek API integration
        services.AddScoped<IBggApiService, BggApiService>();

        return services;
    }

    private static IServiceCollection AddQualityServices(this IServiceCollection services)
    {
        // AI-11: Quality tracking services
        services.AddScoped<IResponseQualityService, ResponseQualityService>();
        services.AddSingleton<QualityMetrics>();
        services.AddSingleton<QualityReportService>();
        services.AddSingleton<IQualityReportService>(sp => sp.GetRequiredService<QualityReportService>());
        services.AddHostedService(sp => sp.GetRequiredService<QualityReportService>());

        return services;
    }
}
