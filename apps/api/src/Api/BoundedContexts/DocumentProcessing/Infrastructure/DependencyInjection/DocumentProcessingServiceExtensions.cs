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
using Api.Services.Pdf;
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
        services.AddScoped<IPdfDeduplicationService, PdfDeduplicationService>(); // Centralized PDF dedup rule (cover-config plan Task 1)

        // Issue #3653: Private PDF progress streaming service (singleton for in-memory subscriber management)
        services.AddSingleton<IPrivatePdfProgressStreamService, PrivatePdfProgressStreamService>();

        // Issue #4209: Generic PDF progress streaming service (supports both public and private PDFs)
        services.AddSingleton<IPdfProgressStreamService, PdfProgressStreamService>();

        // Issue #4732: Queue SSE streaming service (singleton for in-memory subscriber management)
        services.AddSingleton<IQueueStreamService, QueueStreamService>();

        // Issue #2732: Share request document services
        services.AddScoped<IShareRequestDocumentService, ShareRequestDocumentService>();

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

        // #3269 fix: the bulk re-index C2 gate (BulkReindexReadyCommandHandler) injects
        // IPdfExtractorHealthProbe UNCONDITIONALLY, so it must be registered regardless of the selected
        // extractor provider. Previously it was only registered inside RegisterUnstructuredExtractor
        // (Orchestrator/Unstructured providers), so on Docnet/SmolDocling/fallback the reindex-ready
        // endpoint 500'd on DI activation. The probe depends only on IHttpClientFactory (always
        // registered); when the "UnstructuredService" named client is absent (non-Unstructured
        // providers) the probe degrades to "unhealthy" at runtime, so the gate throws a clear
        // ConflictException (409) instead of a 500.
        services.AddScoped<IPdfExtractorHealthProbe, UnstructuredExtractorHealthProbe>();

        // DC-2 (#3419): scoped per-request extraction strategy selector. PdfProcessingPipelineService
        // sets it per PDF and UnstructuredPdfTextExtractor — resolved in the SAME DI scope via the
        // orchestrator's ctor graph — reads it. Registered unconditionally: harmless under
        // non-Unstructured providers (the extractor that reads it simply isn't constructed).
        services.AddScoped<IExtractionStrategySelector, ExtractionStrategySelector>();

        // #3435 (SP1): register the hi_res region extractor + IRawHiResExtractor unconditionally
        // (the seed-batch handler injects it under every provider — see method doc).
        RegisterImageRegionHiResExtractor(services, configuration);

        // #3569: same reasoning for the SmolDoclingService named client — GetPdfPageImageQueryHandler
        // resolves it under every provider, so configuring it only for SmolDocling/Orchestrator left
        // the default (Docnet) deployment with a BaseAddress-less client and a 500 on page-image.
        RegisterSmolDoclingHttpClient(services, configuration);

        if (extractorProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            // BGAI-087 + ISSUE-1174: Register all extractors for orchestrator using keyed services
            // This prevents circular dependency: OrchestratedPdfTextExtractor → EnhancedPdfProcessingOrchestrator → IPdfTextExtractor[]
            RegisterUnstructuredExtractor(services, configuration);
            RegisterSmolDoclingExtractor(services);
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
            RegisterSmolDoclingExtractor(services);
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

        // #3435 (SP4): smoldocling crop-discriminator client for POST /api/v1/extract-image (same
        // app as the SmolDocling extractor, :8002). No Polly — a single crop is quick and the endpoint
        // itself degrades init/non-table failures to a 200 (R5).
        services.AddHttpClient(SmoldoclingTableExtractor.NamedClientKey, client =>
        {
            var baseUrl = configuration["PdfProcessing:Extractor:SmolDocling:ApiUrl"] ?? "http://smoldocling-service:8002";
            client.BaseAddress = new Uri(baseUrl);
            var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:TableExtraction:VlmTimeoutSeconds") ?? 120;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });
        services.AddScoped<ISmolDoclingTableExtractor, SmoldoclingTableExtractor>();

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

        // #3435 (SP4): async VLM table-extraction — arbitrary-page region crop + RAG table-chunk indexing.
        services.AddScoped<IPdfRegionCropper, PdfRegionCropper>();
        services.AddScoped<ITableChunkIndexer, TableChunkIndexer>();

        // Cover-da-PDF plan Task 3 — MaterializePdfCover R2 upload pipeline.
        // Singleton because the underlying AmazonS3Client is thread-safe and
        // reuses a connection pool — matches CoverR2UploadPipeline's lifetime.
        // NOTE: IWebpVariantGenerator is already registered as a Singleton by
        // SharedGameCatalogServiceExtensions.AddSharedGameCatalogContext (same
        // DI container in Program.cs) — not re-registered here to avoid a
        // duplicate registration for the same concrete type.
        RegisterPdfCoverUploadPipeline(services, configuration);

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

        // Issue #2248 (epic #2242, Sub #6 Block B): periodic reconcile for the
        // "Ready ⇒ HasKnowledgeBase" invariant. Runs every 10 minutes and repairs
        // any SharedGame whose inline KB-flag projection was missed.
        RegisterKbFlagDriftAuditJob(services);

        // Issue #3435 (SP1 slice 2): Register Quartz job for the automatic image-region hi_res seed.
        RegisterSeedImageRegionsJob(services, configuration);

        // Issue #3435 (SP4): Register Quartz job for the async VLM table-extraction pass.
        RegisterTableExtractionJob(services, configuration);

        return services;
    }

    /// <summary>
    /// #3435 (SP1 slice 2) — registers <see cref="Api.BoundedContexts.DocumentProcessing.Application.Jobs.SeedImageRegionsJob"/>
    /// with Quartz. Runs every <c>PdfProcessing:ImageRegionSeeding:IntervalMinutes</c> (default 30) to
    /// drive the automatic image-region hi_res seed. The command handler is gated by
    /// <c>PdfProcessing:ImageRegionSeeding:Enabled</c> (default false), so the job is a cheap no-op
    /// (one config check) until the feature is turned on per-environment.
    /// </summary>
    private static void RegisterSeedImageRegionsJob(IServiceCollection services, IConfiguration configuration)
    {
        var intervalMinutes = configuration.GetValue<int?>("PdfProcessing:ImageRegionSeeding:IntervalMinutes") ?? 30;
        if (intervalMinutes < 1)
        {
            intervalMinutes = 30;
        }

        // Only register the job definition + trigger here — do NOT call AddQuartzHostedService
        // (bootstrapped once in the Administration context; a second call would duplicate the scheduler).
        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("SeedImageRegionsJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.SeedImageRegionsJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("SeedImageRegionsTrigger", "DocumentProcessing")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(intervalMinutes)
                    .RepeatForever())
                .WithDescription("Runs the automatic image-region hi_res seed batch (#3435, flag-gated)")
            );
        });
    }

    /// <summary>
    /// #3435 (SP4) — registers <see cref="Api.BoundedContexts.DocumentProcessing.Application.Jobs.RunTableExtractionJob"/>
    /// with Quartz. Runs every <c>PdfProcessing:TableExtraction:IntervalMinutes</c> (default 30) to drive
    /// the async VLM table-extraction batch. The command handler is gated by
    /// <c>PdfProcessing:TableExtraction:Enabled</c> (default false), so the job is a cheap no-op (one
    /// config check) until the feature is turned on per-environment.
    /// </summary>
    private static void RegisterTableExtractionJob(IServiceCollection services, IConfiguration configuration)
    {
        var intervalMinutes = configuration.GetValue<int?>("PdfProcessing:TableExtraction:IntervalMinutes") ?? 30;
        if (intervalMinutes < 1)
        {
            intervalMinutes = 30;
        }

        services.AddQuartz(q =>
        {
            var jobKey = new Quartz.JobKey("RunTableExtractionJob", "DocumentProcessing");

            q.AddJob<Api.BoundedContexts.DocumentProcessing.Application.Jobs.RunTableExtractionJob>(opts =>
                opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("RunTableExtractionTrigger", "DocumentProcessing")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(intervalMinutes)
                    .RepeatForever())
                .WithDescription("Runs the async VLM table-extraction batch (#3435 SP4, flag-gated)")
            );
        });
    }

    /// <summary>
    /// Cover-da-PDF plan Task 3 — registers <see cref="IPdfCoverUploadPipeline"/>
    /// as a singleton backed by a lazily-constructed <see cref="Amazon.S3.IAmazonS3"/>
    /// client. Mirrors <c>SharedGameCatalogServiceExtensions.RegisterCoverR2UploadPipeline</c>
    /// (same <c>S3_*</c> env vars as <see cref="BlobStorageServiceFactory"/>) so the
    /// two R2 cover pipelines don't drift on endpoint/credentials. Singleton
    /// lifetime matches the long-lived AWS SDK client (thread-safe + connection pool).
    /// </summary>
    private static void RegisterPdfCoverUploadPipeline(IServiceCollection services, IConfiguration configuration)
    {
        // Issue #3363: the R2 cover-upload pipeline is a cloud-only, best-effort enrichment. Register it
        // ONLY when STORAGE_PROVIDER=s3 (mirrors BlobStorageServiceFactory). In local-storage mode the
        // service stays unregistered, so PdfProcessingPipelineService's optional IPdfCoverUploadPipeline?
        // resolves to null and cover generation is skipped gracefully (see :562). The previous
        // unconditional registration threw "S3_ENDPOINT is required" at DI resolution — which happens
        // when PdfProcessingPipelineService is constructed, BEFORE the best-effort try/catch could skip
        // the cover — so every PDF upload failed at the Upload step in any local-storage environment
        // (the CI seed-snapshot bake + `make dev` locally).
        var storageProvider = configuration["STORAGE_PROVIDER"]?.ToLowerInvariant() ?? "local";
        if (!string.Equals(storageProvider, "s3", StringComparison.Ordinal))
        {
            return;
        }

        services.AddSingleton<IPdfCoverUploadPipeline>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<Api.BoundedContexts.DocumentProcessing.Infrastructure.Services.PdfCoverUploadPipeline>>();

            var options = new S3StorageOptions
            {
                Endpoint = config["S3_ENDPOINT"] ?? throw new InvalidOperationException("S3_ENDPOINT is required for PdfCoverUploadPipeline"),
                AccessKey = config["S3_ACCESS_KEY"] ?? throw new InvalidOperationException("S3_ACCESS_KEY is required for PdfCoverUploadPipeline"),
                SecretKey = config["S3_SECRET_KEY"] ?? throw new InvalidOperationException("S3_SECRET_KEY is required for PdfCoverUploadPipeline"),
                BucketName = config["S3_BUCKET_NAME"] ?? throw new InvalidOperationException("S3_BUCKET_NAME is required for PdfCoverUploadPipeline"),
                Region = config["S3_REGION"] ?? "auto",
                ForcePathStyle = bool.TryParse(config["S3_FORCE_PATH_STYLE"], out var forcePathStyle) && forcePathStyle,
            };

            var s3Config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region,
            };

            if (!string.Equals(options.Region, "auto", StringComparison.Ordinal)
                && Amazon.RegionEndpoint.GetBySystemName(options.Region) != null)
            {
                s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
            }

            var credentials = new Amazon.Runtime.BasicAWSCredentials(options.AccessKey, options.SecretKey);
            var s3Client = new Amazon.S3.AmazonS3Client(credentials, s3Config);

            // Issue #1357: R2 rejects x-amz-tagging-directive; strip it
            // defensively (same hook used by BlobStorageServiceFactory).
            s3Client.BeforeRequestEvent += BlobStorageServiceFactory.StripUnsupportedR2Headers;

            return new Api.BoundedContexts.DocumentProcessing.Infrastructure.Services.PdfCoverUploadPipeline(s3Client, options, logger);
        });
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

                // 120s (was 35s): large rulebooks with coordinate-aware extraction (epic #3403)
                // legitimately exceed 35s; the staging re-index timed out on ~5 PDFs. appsettings
                // sets 120 explicitly; this is the fallback when config is absent.
                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:TimeoutSeconds") ?? 120;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(
                configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:MaxRetries") ?? 3))
            .AddServiceCallLogging("UnstructuredService");

        // NOTE (#3435): UnstructuredPdfTextExtractor + the hi_res region client + IRawHiResExtractor are
        // registered UNCONDITIONALLY via RegisterImageRegionHiResExtractor in AddDocumentProcessingContext,
        // so they must NOT be registered here (they'd be missing under non-Unstructured providers).
        // IPdfExtractorHealthProbe is likewise registered unconditionally (see the #3269 note there).
    }

    /// <summary>
    /// #3435 (SP1): registers the concrete <see cref="UnstructuredPdfTextExtractor"/>, its dedicated
    /// long-timeout hi_res HttpClient, and <see cref="IRawHiResExtractor"/> UNCONDITIONALLY — mirroring
    /// the #3269 <c>IPdfExtractorHealthProbe</c> fix. <c>RunImageRegionSeedBatchCommandHandler</c>
    /// injects <see cref="IRawHiResExtractor"/>, so it must resolve under EVERY extractor provider,
    /// otherwise the admin seed-batch endpoint 500s on MediatR activation on Docnet/SmolDocling. The
    /// extractor depends only on <c>IHttpClientFactory</c> + the always-registered
    /// <c>IExtractionStrategySelector</c>, so it constructs fine even when it isn't the active extractor.
    /// </summary>
    private static void RegisterImageRegionHiResExtractor(IServiceCollection services, IConfiguration configuration)
    {
        // Dedicated long-timeout client for the ~185-223s hi_res pass (exceeds the 120s ingest client).
        // Deliberately NO retry policy — a single ~200s call must not be retried (the default
        // GetRetryPolicy retries 3x on RequestTimeout, tripling an already-long call and stampeding the
        // service). Retry is at batch granularity: a PDF whose pass fails is left unmarked
        // (ImageRegionsSeededAt == null) and re-selected on the next SeedImageRegionsBatch run.
        services.AddHttpClient(UnstructuredPdfTextExtractor.HiResClientName, client =>
            {
                var apiUrl = configuration["PdfProcessing:Extractor:Unstructured:ApiUrl"]
                             ?? "http://unstructured-service:8001";
                client.BaseAddress = new Uri(apiUrl);

                var hiResTimeoutSeconds = configuration.GetValue<int?>(
                    "PdfProcessing:Extractor:Unstructured:HiResTimeoutSeconds") ?? 300;
                client.Timeout = TimeSpan.FromSeconds(hiResTimeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddServiceCallLogging("UnstructuredServiceHiRes");

        services.AddScoped<UnstructuredPdfTextExtractor>();
        services.AddScoped<IRawHiResExtractor>(sp => sp.GetRequiredService<UnstructuredPdfTextExtractor>());
    }

    /// <summary>
    /// Issue #3569: configures the <c>SmolDoclingService</c> named client. Registered UNCONDITIONALLY
    /// (see caller) because consumers outside the extractor chain resolve it under every provider —
    /// <c>GetPdfPageImageQueryHandler</c> (import-wizard page preview) calls
    /// <c>CreateClient("SmolDoclingService")</c> regardless of <c>PdfProcessing:Extractor:Provider</c>.
    /// When this ran only for the SmolDocling/Orchestrator providers, the default (Docnet) config
    /// handed the handler a client with no <c>BaseAddress</c> and the endpoint answered 500
    /// ("An invalid request URI was provided"). Same class of defect as the DI pitfall in #2565.
    /// Must be called exactly once: <c>AddHttpClient</c> accumulates configuration per name, so a
    /// second call would stack a duplicate retry policy.
    /// </summary>
    private static void RegisterSmolDoclingHttpClient(IServiceCollection services, IConfiguration configuration)
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
    }

    /// <summary>
    /// BGAI-087: Register SmolDocling extractor with new config.
    /// The HTTP client itself is registered unconditionally by
    /// <see cref="RegisterSmolDoclingHttpClient"/> (#3569); this only adds the extractor.
    /// </summary>
    private static void RegisterSmolDoclingExtractor(IServiceCollection services)
    {
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
