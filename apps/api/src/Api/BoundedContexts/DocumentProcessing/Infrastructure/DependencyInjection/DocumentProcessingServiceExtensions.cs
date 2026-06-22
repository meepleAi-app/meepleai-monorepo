using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Extensions;
using Api.Infrastructure.BackgroundServices;
using Api.Services;
using Api.Infrastructure.Http;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;

internal static class DocumentProcessingServiceExtensions
{
    /// <summary>
    /// Keyed service keys for PDF text extractors (ISSUE-1174: Post-merge enhancement)
    /// </summary>
    internal static class PdfExtractorKeys
    {
        public const string Unstructured = "unstructured";
        public const string SmolDocling = "smoldocling";
        public const string Docnet = "docnet";
    }

    public static IServiceCollection AddDocumentProcessingContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // BGAI-086: Register and validate PDF processing configuration on startup
        services.AddOptions<PdfProcessingOptions>()
            .Bind(configuration.GetSection("PdfProcessing"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PdfProcessingOptions>, PdfProcessingConfigurationValidator>();

        // Domain Layer
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IPhotoBatchUploadRepository, PhotoBatchUploadRepository>(); // Libro Game AI Assistant MVP Phase 1
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>(); // Issue #4731: Queue commands
        services.AddScoped<IProcessingQueueConfigRepository, ProcessingQueueConfigRepository>(); // Issue #5455: Queue config
        services.AddScoped<IChunkedUploadSessionRepository, ChunkedUploadSessionRepository>();
        services.AddScoped<IDocumentCollectionRepository, DocumentCollectionRepository>(); // ISSUE-2051: Document collections
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain Services (stateless, can be singleton for performance)
        services.AddSingleton<TableToAtomicRuleConverter>();
        services.AddSingleton<PdfValidationDomainService>(); // PDF-09: Business validation rules
        services.AddScoped<PdfTextProcessingDomainService>(); // DDD-PHASE4: Text processing business rules
        services.AddScoped<PdfQualityValidationDomainService>(); // BGAI-012: Quality threshold enforcement
        services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>(); // User tier-based upload quotas
        services.AddScoped<IUserQuotaInfoService, UserQuotaInfoService>(); // Local read service for user quota info (avoids cross-BC IUserRepository dependency)
        services.AddScoped<IQueueBackpressureService, QueueBackpressureService>(); // Issue #5457: Backpressure
        services.AddScoped<CitationPriorityService>(); // ISSUE-2051: Citation priority and deduplication

        // Issue #3653: Private PDF progress streaming service (singleton for in-memory subscriber management)
        services.AddSingleton<IPrivatePdfProgressStreamService, PrivatePdfProgressStreamService>();

        // Issue #4209: Generic PDF progress streaming service (supports both public and private PDFs)
        services.AddSingleton<IPdfProgressStreamService, PdfProgressStreamService>();

        // Issue #4732: Queue SSE streaming service (singleton for in-memory subscriber management)
        services.AddSingleton<IQueueStreamService, QueueStreamService>();

        // Issue #2732: Share request document services
        services.AddScoped<IShareRequestDocumentService, ShareRequestDocumentService>();
        services.AddScoped<IStorageQuotaService, StorageQuotaService>();

        // Issue #4212: Processing metrics and ETA calculation service
        services.AddScoped<IProcessingMetricsService, ProcessingMetricsService>();

        // Issue #5445: Language detection for PDF pipeline routing
        services.AddSingleton<ILanguageDetector, LanguageDetector>();

        // Libro Game AI Assistant Phase 2 — Task 2.4: Q&A complexity classifier (stateless, Singleton-safe)
        services.AddSingleton<IQAComplexityClassifier, HeuristicQAComplexityClassifier>();

        // RAG translation: LLM-based chunk translation for cross-language retrieval
        services.AddScoped<IChunkTranslationService, ChunkTranslationService>();

        // Infrastructure Adapters (scoped - may use file I/O)
        services.AddScoped<IPdfTableExtractor, ITextPdfTableExtractor>();
        services.AddScoped<IPdfValidator, DocnetPdfValidator>(); // PDF-09: DDD validation adapter
        services.AddScoped<IBggGameExtractor, BggGameExtractor>(); // ISSUE-2513: BGG games PDF extraction

        // BGAI-086/087: Configure PDF text extractor based on provider setting
        var extractorProvider = configuration["PdfProcessing:Extractor:Provider"] ?? "Orchestrator";

        if (extractorProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            // BGAI-087 + ISSUE-1174: Register all extractors for orchestrator using keyed services
            // This prevents circular dependency: OrchestratedPdfTextExtractor → EnhancedPdfProcessingOrchestrator → IPdfTextExtractor[]
            RegisterUnstructuredExtractor(services, configuration);
            RegisterSmolDoclingExtractor(services, configuration);
            services.AddScoped<DocnetPdfTextExtractor>();

            // ISSUE-1174: Register stage extractors as keyed services (avoids circular DI dependency)
            // The orchestrator constructor uses [FromKeyedServices] to resolve specific extractors
            services.AddKeyedScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>(PdfExtractorKeys.Unstructured);
            services.AddKeyedScoped<IPdfTextExtractor, SmolDoclingPdfTextExtractor>(PdfExtractorKeys.SmolDocling);
            services.AddKeyedScoped<IPdfTextExtractor, DocnetPdfTextExtractor>(PdfExtractorKeys.Docnet);

            // Register ITextChunkingService required by EnhancedPdfProcessingOrchestrator
            services.AddScoped<ITextChunkingService, TextChunkingService>();

            // Register orchestrator application service
            services.AddScoped<EnhancedPdfProcessingOrchestrator>();

            // Register orchestrator adapter as primary extractor interface
            services.AddScoped<IPdfTextExtractor, OrchestratedPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("Unstructured", StringComparison.OrdinalIgnoreCase))
        {
            RegisterUnstructuredExtractor(services, configuration);
            services.AddScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("SmolDocling", StringComparison.OrdinalIgnoreCase))
        {
            RegisterSmolDoclingExtractor(services, configuration);
            services.AddScoped<IPdfTextExtractor, SmolDoclingPdfTextExtractor>();
        }
        else
        {
            // Fallback: Docnet extractor
            services.AddScoped<IPdfTextExtractor, DocnetPdfTextExtractor>();
        }

        // Libro Game AI Assistant MVP Phase 1 — Task 1.4b
        // Photo preprocessor HTTP adapter (no Polly: simple timeout sufficient for Sprint 1)
        services.AddHttpClient("smoldocling-photo-preprocessor", client =>
        {
            var baseUrl = configuration["SMOLDOCLING_SERVICE_URL"] ?? "http://smoldocling-service:8500";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IPhotoPreprocessor, SmoldoclingPhotoPreprocessor>();

        // Libro Game AI Assistant MVP Phase 2 — Task 2.3a: KB Indexing Services
        services.AddScoped<IDocumentChunker, PageTextChunker>();
        services.AddScoped<IKnowledgeBaseIndexer, KnowledgeBaseIndexer>();

        // Issue #747 PR-C: narrative paragraph-number extraction from OCR text.
        // Stateless + compiled regex → safe as Singleton; reused across all batches.
        services.AddSingleton<IParagraphNumberExtractor, RegexParagraphNumberExtractor>();

        // Libro Game AI Assistant MVP Phase 1 — Task 1.6: parallel photo batch processor
        services.AddScoped<IPhotoBatchProcessor, PhotoBatchProcessor>();

        // Issue #892: Atomic PDF claim service — raw SQL UPDATE against PostgreSQL
        // for production. Tests inject the InMemoryPdfClaimService helper directly.
        services.AddScoped<IPdfClaimService, RelationalPdfClaimService>();

        // Issue #1831 (umbrella #1821 L4) — PDF first-page cover extraction
        services.AddScoped<IPdfCoverExtractor, PdfCoverExtractor>();

        // Shared PDF processing pipeline (used by recovery job and future handler consolidation)
        services.AddScoped<IPdfProcessingPipelineService, PdfProcessingPipelineService>();

        // Stale PDF recovery: runs once on startup to reprocess stuck PDFs
        services.AddHostedService<StalePdfRecoveryService>();

        // Issue #5460: Queue monitoring for proactive alerts (stuck docs, depth, failure rate)
        services.AddHostedService<ProcessingQueueMonitorService>();

        // Issue #4208: Register Quartz job for automatic PDF retry (every 5 minutes)
        RegisterRetryFailedPdfsJob(services);

        // Issue #4212: Register Quartz job for metrics maintenance (hourly)
        RegisterMetricsMaintenanceJob(services);

        // Issue #4730: Register Quartz job for PDF processing queue (every 10 seconds)
        RegisterPdfProcessingQueueJob(services);

        // Issue #1831: Register Quartz job for L4 PDF cover backfill (every 30 minutes)
        RegisterBackfillPdfCoversJob(services);

        // Issue #1831 follow-up: Register Quartz job for orphan cover recovery (daily at 03:00 UTC)
        RegisterPdfCoverOrphanRecoveryJob(services);

        // Issue #2248 (epic #2242, Sub #6 Block B): periodic audit for the
        // "Ready ⇒ HasKnowledgeBase" invariant. Runs every 10 minutes.
        RegisterKbFlagDriftAuditJob(services);

        return services;
    }

    /// <summary>
    /// Issue #1831 — registers <see cref="Api.BoundedContexts.DocumentProcessing.Application.Jobs.BackfillPdfCoversJob"/>
    /// with the Quartz scheduler. Runs every 30 minutes to pick up PDFs that
    /// completed ingestion before the L4 cover stack shipped (or whose cover
    /// step was deferred) and generate their cover image lazily.
    /// </summary>
    private static void RegisterBackfillPdfCoversJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("BackfillPdfCoversJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.BackfillPdfCoversJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("BackfillPdfCoversTrigger", "DocumentProcessing")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(30)
                    .RepeatForever())
                .WithDescription("Backfills L4 PDF covers for PDFs whose ingestion completed without a cover")
            );
        });
    }

    /// <summary>
    /// Issue #1831 follow-up — registers <see cref="Api.BoundedContexts.DocumentProcessing.Application.Jobs.PdfCoverOrphanRecoveryJob"/>
    /// with the Quartz scheduler. Runs daily at 03:00 UTC to scan PDFs with
    /// <c>CoverGenerationStatus=Generated</c> whose R2 object is missing and
    /// reset them to <c>Pending</c> for re-generation by <see cref="Api.BoundedContexts.DocumentProcessing.Application.Jobs.BackfillPdfCoversJob"/>.
    /// </summary>
    private static void RegisterPdfCoverOrphanRecoveryJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("PdfCoverOrphanRecoveryJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.PdfCoverOrphanRecoveryJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("PdfCoverOrphanRecoveryTrigger", "DocumentProcessing")
                .WithCronSchedule("0 0 3 * * ?") // Daily at 03:00 UTC
                .WithDescription("Detects Generated PDF covers whose R2 object is missing and resets them to Pending for re-generation")
            );
        });
    }

    /// <summary>
    /// Issue #4208: Register RetryFailedPdfsJob with Quartz scheduler.
    /// Runs every 5 minutes to automatically retry failed PDFs with retriable errors.
    /// </summary>
    private static void RegisterRetryFailedPdfsJob(IServiceCollection services)
    {
        // Only register job definition here - do NOT call AddQuartzHostedService (would duplicate).
        // The AddQuartzHostedService is called once in Administration context.
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("RetryFailedPdfsJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.RetryFailedPdfsJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("RetryFailedPdfsTrigger", "DocumentProcessing")
                .WithCronSchedule("0 */5 * * * ?") // Every 5 minutes
                .WithDescription("Automatically retries failed PDF processing with exponential backoff")
            );
        });
    }

    /// <summary>
    /// Issue #4212: Register MetricsMaintenanceJob with Quartz scheduler.
    /// Runs hourly to cleanup old metrics and maintain historical data.
    /// </summary>
    private static void RegisterMetricsMaintenanceJob(IServiceCollection services)
    {
        // Only register job definition here - do NOT call AddQuartzHostedService (would duplicate).
        // The AddQuartzHostedService is called once in Administration context.
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("MetricsMaintenanceJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.MetricsMaintenanceJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("MetricsMaintenanceTrigger", "DocumentProcessing")
                .WithCronSchedule("0 0 * * * ?") // Hourly (at the top of every hour)
                .WithDescription("Cleans up old metrics and maintains historical data for ETA calculation")
            );
        });
    }

    /// <summary>
    /// Issue #4730: Register PdfProcessingQuartzJob with Quartz scheduler.
    /// Runs every 10 seconds to pick up and process queued PDFs.
    /// Max 3 concurrent executions controlled via Quartz thread pool (configured globally).
    /// </summary>
    private static void RegisterPdfProcessingQueueJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("PdfProcessingQuartzJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.PdfProcessingQuartzJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("PdfProcessingQueueTrigger", "DocumentProcessing")
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(10)
                    .RepeatForever())
                .WithDescription("Picks up and processes the next queued PDF every 10 seconds")
            );
        });
    }

    /// <summary>
    /// Issue #2248 (epic #2242 Sub #6 Block B): periodic guard against the
    /// silent-failure mode where PdfDocument.ProcessingState=Ready but
    /// SharedGame.HasKnowledgeBase=false. Increments
    /// <c>meepleai.pdf.indexed.no.kb.flag.total</c> for each drifted row.
    /// SLO=0; any increment is a P1 alert.
    /// </summary>
    private static void RegisterKbFlagDriftAuditJob(IServiceCollection services)
    {
        // Only register job definition here — AddQuartzHostedService is bootstrapped
        // once in the Administration context.
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("KbFlagDriftAuditJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.KbFlagDriftAuditJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("KbFlagDriftAuditTrigger", "DocumentProcessing")
                .WithCronSchedule("0 */10 * * * ?") // Every 10 minutes
                .WithDescription("Audits Ready PDFs against SharedGame.HasKnowledgeBase invariant (#2248)")
            );
        });
    }

    /// <summary>
    /// BGAI-086: Register Unstructured extractor with updated config paths
    /// </summary>
    private static void RegisterUnstructuredExtractor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("UnstructuredService", client =>
            {
                var apiUrl = configuration["PdfProcessing:Extractor:Unstructured:ApiUrl"]
                             ?? "http://unstructured-service:8001";
                client.BaseAddress = new Uri(apiUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:TimeoutSeconds") ?? 35;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(
                configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:MaxRetries") ?? 3))
            .AddServiceCallLogging("UnstructuredService");

        services.AddScoped<UnstructuredPdfTextExtractor>();
    }

    /// <summary>
    /// BGAI-087: Register SmolDocling extractor with new config
    /// </summary>
    private static void RegisterSmolDoclingExtractor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("SmolDoclingService", client =>
            {
                var apiUrl = configuration["PdfProcessing:Extractor:SmolDocling:ApiUrl"]
                             ?? "http://smoldocling-service:8002";
                client.BaseAddress = new Uri(apiUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:SmolDocling:TimeoutSeconds") ?? 30;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(
                configuration.GetValue<int?>("PdfProcessing:Extractor:SmolDocling:MaxRetries") ?? 3))
            .AddServiceCallLogging("SmolDoclingService");

        services.AddScoped<SmolDoclingPdfTextExtractor>();
    }

    /// <summary>
    /// Get Polly retry policy with configurable max retries
    /// </summary>
    private static Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetries)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
