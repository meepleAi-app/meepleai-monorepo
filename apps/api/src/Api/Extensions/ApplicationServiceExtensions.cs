using Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;
using Api.BoundedContexts.Authentication.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.GameManagement.Infrastructure.DependencyInjection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;
using Api.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.DependencyInjection;
using Api.Helpers;
using Api.Services;
using Api.Services.Pdf;
using Api.Services.Qdrant;
using Api.Services.Rag;
using Api.Observability;
using FluentValidation;

namespace Api.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddVectorSearchServices(configuration);
        services.AddDomainServices();
        services.AddAiServices();
        services.AddPdfServices();
        services.AddChatServices();
        services.AddAdminServices();
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

        // DDD-PHASE3: Administration bounded context
        services.AddAdministrationContext();

        // DDD-PHASE4: DocumentProcessing bounded context
        // BGAI-001-v2: Pass configuration for PDF extractor provider selection
        services.AddDocumentProcessingContext(configuration);

        // ISSUE-2053: UserNotifications bounded context
        services.AddUserNotificationsContext();

        return services;
    }

    private static IServiceCollection AddVectorSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AI-01: Vector search services
        services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();

        // Qdrant specialized services (SOLID refactoring)
        services.AddScoped<IQdrantCollectionManager, QdrantCollectionManager>();
        services.AddScoped<IQdrantVectorIndexer, QdrantVectorIndexer>();
        services.AddScoped<IQdrantVectorSearcher, QdrantVectorSearcher>();

        // Qdrant facade service (Scoped to match specialized services lifetime)
        services.AddScoped<IQdrantService, QdrantService>();

        // ADR-016 Phase 2: Multi-provider embedding configuration
        services.Configure<EmbeddingConfiguration>(configuration.GetSection("Embedding"));

        // ADR-016 Phase 2: Embedding provider factory for multi-provider support
        services.AddScoped<IEmbeddingProviderFactory, EmbeddingProviderFactory>();

        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Game and RuleSpec services
        // Issue #1185: RuleSpecService migrated to CQRS pattern in GameManagement bounded context
        // Issue #1189: RuleSpec Comment/Diff services migrated to CQRS pattern
        services.AddScoped<Api.BoundedContexts.GameManagement.Domain.Services.RuleSpecDiffDomainService>();

        // CONFIG-01: Dynamic configuration service (still used by legacy services/helpers)
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // CONFIG: Configuration wrapper (TEST-900 RC-1: enables mocking of IConfiguration extension methods)
        services.AddSingleton<IConfigurationWrapper>(sp =>
            new ConfigurationWrapper(sp.GetRequiredService<IConfiguration>()));

        // CONFIG: Configuration helper for 3-tier fallback (Database → Config → Defaults)
        services.AddScoped<ConfigurationHelper>();

        // CONFIG-05: Feature flags service
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // N8N services
        services.AddScoped<N8NConfigService>();
        services.AddScoped<N8NTemplateService>(); // N8N-04: Workflow template service

        // N8N-05: Workflow error logging service
        services.AddScoped<IWorkflowErrorLoggingService, WorkflowErrorLoggingService>();

        return services;
    }

    private static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        // ISSUE-958: ILlmService now registered in KnowledgeBaseServiceExtensions as HybridLlmService
        // (Removed old LlmService registration to prevent duplicate)

        // AI-09: Language detection for multi-language support
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();

        // AI-05: AI response caching (PERF-03: Changed from Singleton to Scoped due to MeepleAiDbContext dependency)
        services.AddScoped<IAiResponseCacheService, AiResponseCacheService>();

        // SOLID Phase 3: RAG sub-services (specialized services extracted from RagService)
        services.AddScoped<IQueryExpansionService, QueryExpansionService>();
        services.AddScoped<ISearchResultReranker, SearchResultReranker>();
        services.AddScoped<ICitationExtractorService, CitationExtractorService>();
        services.AddScoped<IRagConfigurationProvider, RagConfigurationProvider>(); // Issue #1441: RAG configuration provider

        // RAG and search services
        services.AddScoped<IRagService, RagService>(); // AI-04: RAG service (now a facade over specialized sub-services)
        services.AddScoped<IKeywordSearchService, KeywordSearchService>(); // AI-14: PostgreSQL full-text keyword search
        services.AddScoped<IHybridSearchService, HybridSearchService>(); // AI-14: Hybrid search with RRF fusion

        // Issue #1186: Streaming services migrated to CQRS handlers
        // Removed: IStreamingRagService, IStreamingQaService, SetupGuideService
        // Now using: StreamExplainQueryHandler, StreamQaQueryHandler, StreamSetupGuideQueryHandler

        // Setup and prompt services
        services.AddScoped<IPromptTemplateService, PromptTemplateService>(); // AI-07.1: Prompt template service
        services.AddScoped<IPromptEvaluationService, PromptEvaluationService>(); // ADMIN-01 Phase 4: Prompt evaluation

        // AI-06: RAG offline evaluation service
        services.AddScoped<IRagEvaluationService, RagEvaluationService>();

        // AI-07: Prompt versioning and management service

        // Audit and logging
        services.AddScoped<AuditService>();
        // AiRequestLogService migrated to CQRS (LogAiRequestCommand)

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
        // DDD-COMPLETE: PDF services fully migrated to CQRS in DocumentProcessing bounded context

        // Issue #940 Phase 3: DDD PDF text extraction adapter
        services.AddSingleton<BoundedContexts.DocumentProcessing.Domain.Services.PdfTextProcessingDomainService>();
        services.AddScoped<BoundedContexts.DocumentProcessing.Infrastructure.External.IPdfTextExtractor,
            BoundedContexts.DocumentProcessing.Infrastructure.External.DocnetPdfTextExtractor>();

        // PDF-02: OCR service for fallback text extraction (Windows-only)
#pragma warning disable CA1416 // TesseractOcrAdapter is Windows-only by design
        services.AddSingleton<BoundedContexts.DocumentProcessing.Infrastructure.External.IOcrService, BoundedContexts.DocumentProcessing.Infrastructure.External.TesseractOcrAdapter>();
#pragma warning restore CA1416

        return services;
    }

    private static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        // CHAT-05: Chat export services
        services.AddScoped<IChatExportService, ChatExportService>();
        services.AddScoped<IExportFormatter, TxtExportFormatter>();
        services.AddScoped<IExportFormatter, MdExportFormatter>();
        services.AddScoped<IExportFormatter, PdfExportFormatter>();

        return services;
    }

    private static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        // ADMIN-01: User management - MIGRATED TO DDD/CQRS (handlers in Administration bounded context)
        // UserManagementService REMOVED (243 lines eliminated)

        // ADMIN-02: Analytics dashboard service (still used by handlers as infrastructure)
        services.AddScoped<IAdminStatsService, AdminStatsService>();

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

        // BGAI-042: Weekly automated quality evaluation
        services.AddHostedService<WeeklyEvaluationService>();

        return services;
    }

    /// <summary>
    /// Registers FluentValidation validators for CQRS pipeline validation.
    /// Issue #1449: FluentValidation for Authentication bounded context.
    /// </summary>
    public static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        // Register all validators from the Authentication bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.Authentication.Application.Validators.LoginCommandValidator>();

        return services;
    }
}
