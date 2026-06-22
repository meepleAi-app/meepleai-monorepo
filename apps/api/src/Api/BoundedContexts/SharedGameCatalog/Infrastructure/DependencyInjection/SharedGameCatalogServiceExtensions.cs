using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddScoped<IMechanicAnalysisRepository, MechanicAnalysisRepository>(); // Issue #523: M1.1 Mechanic Analysis persistence
        services.AddScoped<IMechanicGoldenClaimRepository, MechanicGoldenClaimRepository>(); // ADR-051 Sprint 1 / Task 15: golden claims
        services.AddScoped<IMechanicGoldenBggTagRepository, MechanicGoldenBggTagRepository>(); // ADR-051 Sprint 1 / Task 15: BGG mechanic tags
        services.AddScoped<IMechanicAnalysisMetricsRepository, MechanicAnalysisMetricsRepository>(); // ADR-051 Sprint 1 / Task 15: metrics snapshots
        services.AddScoped<ICertificationThresholdsConfigRepository, CertificationThresholdsConfigRepository>(); // ADR-051 Sprint 1 / Task 15: thresholds config
        services.AddScoped<IMechanicRecalcJobRepository, MechanicRecalcJobRepository>(); // ADR-051 Sprint 2 / Task 7: async recalc job persistence + SKIP LOCKED claim
        services.AddScoped<ICatalogSyncRunRepository, CatalogSyncRunRepository>(); // #1861: F4-A6 catalog sync run history
        services.AddScoped<IEnrichmentQueueRepository, EnrichmentQueueRepository>(); // #1874: queued BGG enrichment requests
        services.AddScoped<IEnrichmentAttemptRepository, EnrichmentAttemptRepository>(); // #1874: BGG enrichment outcome history
        services.AddScoped<IWikidataCoverEnrichmentAttemptRepository, WikidataCoverEnrichmentAttemptRepository>(); // #1823 M9: Wikidata cover enrichment outcome history
        services.AddScoped<ICatalogSeedDraftRepository, CatalogSeedDraftRepository>(); // #1903 M1.5
        services.AddScoped<IShareRequestRepository, ShareRequestRepository>(); // Issue #2724: CreateShareRequest
        services.AddScoped<IBadgeRepository, BadgeRepository>(); // Issue #2731: Badge gamification system
        services.AddScoped<IUserBadgeRepository, UserBadgeRepository>(); // Issue #2731: User badge awards
        services.AddScoped<IContributorRepository, ContributorRepository>(); // Issue #2735: Contributor stats endpoints
        services.AddScoped<IGameAnalyticsEventRepository, GameAnalyticsEventRepository>(); // Issue #3918: Trending analytics
        services.AddScoped<ISharedGameTranslationRepository, SharedGameTranslationRepository>(); // Issue #2339: Shared game non-EN translations
        services.AddScoped<IGameTitleResolver, GameTitleResolver>(); // Issue #2339: Batch translation enricher for list queries

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

        // ISSUE-524 / M1.2: Mechanic Extractor services (ADR-051).
        // Pipeline flow: handler → create Draft aggregate → enqueue IMechanicAnalysisExecutor
        // on a fresh DI scope → executor loads aggregate, runs IMechanicAnalysisPipeline,
        // parses with MechanicOutputParser, persists, publishes domain events.
        // Singleton: prompts are embedded assembly resources + internal ConcurrentDictionary cache.
        // Scoped would silently defeat the cache (new instance per request).
        services.AddSingleton<IMechanicPromptProvider, EmbeddedMechanicPromptProvider>();
        services.AddScoped<IMechanicOutputValidator, MechanicOutputValidator>();
        services.AddScoped<IAnalysisCostEstimator, AnalysisCostEstimator>();
        services.AddScoped<IMechanicAnalysisPipeline, MechanicAnalysisPipeline>();
        services.AddScoped<IMechanicAnalysisExecutor, MechanicAnalysisExecutor>();

        // ADR-051 Sprint 1 / Task 18: AI comprehension validation - matching engine + keyword extractor.
        // Consumed by CalculateMechanicAnalysisMetricsCommand to compute coverage/page-accuracy/BGG-match
        // percentages by comparing analysis claims against curated golden claims + BGG mechanic tags.
        services.AddScoped<IKeywordExtractor, BagOfWordsKeywordExtractor>();
        services.AddScoped<IMechanicMatchingEngine, MechanicMatchingEngine>();

        // ADR-051 Sprint 1: Bounded-context embedding adapter.
        // Wraps Api.Services.IEmbeddingService to satisfy the narrow domain abstraction
        // consumed by the golden-claim CRUD handlers and CalculateMechanicAnalysisMetricsHandler.
        // CLAUDE.md pitfall #2565: register both the interface and the implementation.
        services.AddScoped<IEmbeddingService, EmbeddingServiceAdapter>();

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

            // Issue #613: Register scheduled bulk invalidation job — defensive
            // safety net catching any cache staleness left behind by a permanent
            // event-handler retry exhaustion (e.g. multi-minute Redis outage).
            q.AddJob<ScheduledBulkInvalidationJob>(opts => opts
                .WithIdentity("scheduled-bulk-invalidation-job", "shared-game-catalog")
                .StoreDurably(true));

            // Trigger: Run every 15 minutes
            q.AddTrigger(opts => opts
                .ForJob("scheduled-bulk-invalidation-job", "shared-game-catalog")
                .WithIdentity("scheduled-bulk-invalidation-trigger", "shared-game-catalog")
                .WithCronSchedule("0 */15 * * * ?")
                .WithDescription("Runs every 15 min to evict shared-game detail tags for top-N most-viewed games and the search-games list tag"));
        });

        // Gap G2: BGG cover re-upload service (typed HttpClient pattern).
        // 10s timeout — BGG CDN images are typically < 500 KB.
        services.AddHttpClient<IBggCoverDownloader, BggCoverDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Issue #1903 M5.2: register HttpClients, keyed catalog providers,
        // aggregator, and Quartz CatalogSeedFetchJob.
        RegisterCatalogSeedProviders(services);
        RegisterCatalogSeedFetchJob(services);

        // Issue #1903 M7.1: monthly BGG ToS hash watcher (spec §8.5.6).
        RegisterBggTosWatcherJob(services);

        // Issue #1903 M6.1: in-memory SSE event broadcaster for the admin
        // catalog seed pipeline. Singleton so the Quartz job publisher and
        // HTTP SSE subscribers share state across the process.
        services.AddSingleton<ICatalogSeedStreamService, CatalogSeedStreamService>();

        // Issue #1903 M7.2: kill-switch feature flag backed by IConfigurationService.
        // Scoped to match the lifetime of the underlying IConfigurationService.
        services.AddScoped<ICatalogSeedFeatureFlag, CatalogSeedFeatureFlag>();

        // Issue #1823 Phase B (ADR DEC-3e): single shared Wikimedia rate-limiter
        // coordinates the 5 RPS cap across Wikidata SPARQL + Commons API consumers.
        // Singleton — token bucket state is process-scoped; DEC-3e locks single-pod
        // batch (HPA=1), so an in-memory limiter is sufficient.
        services.AddSingleton<IWikimediaRateLimiter, InMemoryWikimediaRateLimiter>();

        // Issue #1823 Phase B M6 (ADR DEC-3d + DEC-3d-1 LOCKED 2026-06-20, issue #2055):
        // WebP variant generator backed by Magick.NET-Q8-AnyCPU 14.x (Apache 2.0,
        // ~70MB native deps) — replaced SixLabors.ImageSharp 3.1.12 which switched
        // to the Six Labors Split License (commercial use requires paid license).
        // Produces 200x300 aspect-ratio-preserving center crops at WebP quality 85
        // for R2-hosted cover thumbnails. Singleton — the generator is stateless
        // and Magick.NET's encoder/decoder pipeline is thread-safe.
        services.AddSingleton<IWebpVariantGenerator, WebpVariantGenerator>();

        // Issue #1823 Phase B (ADR DEC-3h): R2 cover upload pipeline. Singleton
        // because the underlying AmazonS3Client is thread-safe and reuses
        // a connection pool — matching the BlobStorageServiceFactory lifetime.
        // CLAUDE.md pitfall #2565: register both the interface and the
        // implementation. NOTE: this registration constructs the AmazonS3Client
        // lazily inside the factory delegate; if STORAGE_PROVIDER is not "s3",
        // the pipeline is still registered but UploadAsync will throw at first
        // call (S3 misconfiguration surfaces synchronously instead of silently).
        RegisterCoverR2UploadPipeline(services);

        // Issue #1823 Wave 3 M9 (ADR DEC-3j): retry/dead-letter classifier for
        // the WikidataCoverEnrichmentJob scheduler. Stateless + thread-safe —
        // singleton lifetime.
        services.AddSingleton<IWikidataCoverEnrichmentRetryPolicy, WikidataCoverEnrichmentRetryPolicy>();

        // Issue #1823 Wave 3 M12: single source of truth for the enrich+record
        // workflow (M9 scheduler + M12 admin trigger endpoint). Scoped — uses
        // the request UoW + repository.
        services.AddScoped<IWikidataCoverEnrichmentRunner, WikidataCoverEnrichmentRunner>();

        // Issue #1823 Phase E F4 + Issue #2256: SSE pub/sub for live admin
        // dead-letter row updates. Default = in-memory (single-pod, ADR DEC-3e).
        // Setting WIKIDATA_SSE_BACKPLANE=redis switches to the Redis pub/sub
        // backplane for multi-pod fan-out — the SSE endpoint contract and
        // publisher (M9 scheduler / M12 trigger) are unchanged.
        services.AddWikidataEnrichmentEventBroadcaster(configuration);

        // Issue #2470 (DEC-C): FE↔BE correlation for the SSE starvation
        // alert. The wikidata-dead-letters page POSTs a heartbeat every 30s
        // while open; the tracker exposes the count via the
        // meepleai_wikidata_sse_admin_clients_connected gauge. Singleton so
        // the TTL'd map survives across requests.
        services.AddSingleton<IWikidataAdminClientHeartbeatTracker, WikidataAdminClientHeartbeatTracker>();

        // Issue #2470: hosted service that binds the static
        // MeepleAiMetrics SSE gauges to runtime sources (broadcaster +
        // tracker) once DI is wired. See class remarks re: idempotent re-bind.
        services.AddHostedService<WikidataSseGaugeBinder>();

        // Issue #1823 Wave 3 M9: Quartz scheduler for batch Wikidata cover
        // enrichment. Runs every 1 minute (HPA=1 per DEC-3e). [DisallowConcurrentExecution]
        // on the job class is the belt-and-braces guarantee that we never have
        // two ticks executing simultaneously.
        RegisterWikidataCoverEnrichmentJob(services);

        // Issue #1823 Wave 3 retention sweep (ADR DEC-3j): Quartz job that
        // deletes dead-letter rows older than 7 days. Runs daily at 03:00 UTC.
        RegisterWikidataCoverDeadLetterRetentionJob(services);

        // Issue #1823 Wave 3 M15 (ADR DEC-3i): Quartz quarterly cron that
        // resets WikidataQidLastVerifiedAt on stale games (>90gg) so the M9
        // scheduler picks them up for re-enrichment. Runs every 3 months at
        // 04:00 UTC.
        RegisterWikidataQuarterlyReVerificationJob(services);

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }

    /// <summary>
    /// Issue #1823 Phase B (ADR DEC-3h) — registers
    /// <see cref="ICoverR2UploadPipeline"/> as a singleton backed by a
    /// lazily-constructed <see cref="Amazon.S3.IAmazonS3"/> client. The S3
    /// client is built from the same <c>S3_*</c> env vars used by
    /// <see cref="BlobStorageServiceFactory"/> so we don't drift on
    /// endpoint/credentials between the two consumers. Singleton lifetime
    /// matches the long-lived AWS SDK client (thread-safe + connection pool).
    /// </summary>
    private static void RegisterCoverR2UploadPipeline(IServiceCollection services)
    {
        services.AddSingleton<ICoverR2UploadPipeline>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<CoverR2UploadPipeline>>();

            var options = new S3StorageOptions
            {
                Endpoint = config["S3_ENDPOINT"] ?? throw new InvalidOperationException("S3_ENDPOINT is required for CoverR2UploadPipeline"),
                AccessKey = config["S3_ACCESS_KEY"] ?? throw new InvalidOperationException("S3_ACCESS_KEY is required for CoverR2UploadPipeline"),
                SecretKey = config["S3_SECRET_KEY"] ?? throw new InvalidOperationException("S3_SECRET_KEY is required for CoverR2UploadPipeline"),
                BucketName = config["S3_BUCKET_NAME"] ?? throw new InvalidOperationException("S3_BUCKET_NAME is required for CoverR2UploadPipeline"),
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

            return new CoverR2UploadPipeline(s3Client, options, logger);
        });
    }

    /// <summary>
    /// Issue #1903 M5.2 — registers Wikidata SPARQL + BGG XMLAPI2 HttpClients with
    /// the spec-mandated <c>User-Agent</c> (admin-catalog-seed; abuse@meepleai.app),
    /// then exposes the providers as keyed <see cref="ICatalogProvider"/> services
    /// (<c>"wikidata"</c> primary, <c>"bgg"</c> fallback) so
    /// <see cref="ICatalogSeedAggregator"/> can resolve them explicitly without
    /// relying on registration order.
    /// </summary>
    /// <remarks>
    /// Spec: 2026-06-04-admin-catalog-seed-design.md §7. The User-Agent string is
    /// mandatory per BGG ToS so they can reach out before legal escalation.
    /// </remarks>
    private static void RegisterCatalogSeedProviders(IServiceCollection services)
    {
        const string CatalogUserAgent = "MeepleAI/1.0 (admin-catalog-seed; abuse@meepleai.app)";

        // Spec-mandated public endpoints (2026-06-04-admin-catalog-seed-design.md §7).
        // S1075 suppressed: these are stable third-party service URLs, not internal infra.
#pragma warning disable S1075 // URIs should not be hardcoded
        const string WikidataBaseUrl = "https://query.wikidata.org/";
        const string BggBaseUrl = "https://boardgamegeek.com/";
#pragma warning restore S1075

        services.AddHttpClient<WikidataCatalogProvider>(client =>
        {
            client.BaseAddress = new Uri(WikidataBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CatalogUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        // Issue #1823 Wave 3 M10 (ADR DEC-3f): circuit breaker per-client.
        // Wikidata SPARQL outage MUST NOT collapse Commons traffic and vice
        // versa — separate handler instances per typed client.
        .AddHttpMessageHandler(sp => new WikimediaCircuitBreakerHandler(
            sp.GetRequiredService<ILogger<WikimediaCircuitBreakerHandler>>(),
            clientName: "wikidata-sparql"));

        // Issue #1823 M4 (ADR DEC-3b/3c/3e): Wikimedia Commons license fetcher.
        // Consumed by the M8 orchestrator AFTER the Wikidata SPARQL pass resolves
        // a wdt:P18 IRI to a filename. Shares the 5 RPS IWikimediaRateLimiter
        // (registered as Singleton above) with WikidataCatalogProvider so the
        // combined SPARQL + Commons traffic stays under the cap. The S1075
        // suppression below covers the public Commons API endpoint just like
        // the other catalog providers.
        //
        // Issue #1823 M8 invariant: do NOT add a ConfigurePrimaryHttpMessageHandler
        // that sets AllowAutoRedirect=false. FetchImageBytesAsync calls
        // commons.wikimedia.org/wiki/Special:FilePath/{filename}, which returns
        // a 302 redirect to upload.wikimedia.org/<cdn-url>. The default handler
        // AllowAutoRedirect=true is what makes the image bytes accessible — a
        // false setting would surface the 302 HTML body as bytes, which would
        // then fail at WebP decoding (FailReasonImageProcessing) instead of the
        // expected SkipReasonImageBytesNotAvailable on real 404s.
#pragma warning disable S1075 // URIs should not be hardcoded
        const string CommonsBaseUrl = "https://commons.wikimedia.org/";
#pragma warning restore S1075
        services.AddHttpClient<IWikimediaCommonsClient, WikimediaCommonsClient>(client =>
        {
            client.BaseAddress = new Uri(CommonsBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CatalogUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        // Issue #1823 Wave 3 M10 (ADR DEC-3f): separate circuit breaker instance
        // from the Wikidata SPARQL client so a Commons CDN outage opens this
        // breaker independently.
        .AddHttpMessageHandler(sp => new WikimediaCircuitBreakerHandler(
            sp.GetRequiredService<ILogger<WikimediaCircuitBreakerHandler>>(),
            clientName: "commons-api"));

        services.AddHttpClient<BggCatalogProvider>(client =>
        {
            client.BaseAddress = new Uri(BggBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CatalogUserAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Keyed registration so CatalogSeedAggregator can resolve "wikidata" (primary)
        // and "bgg" (fallback) explicitly — avoids relying on registration order.
        services.AddKeyedScoped<ICatalogProvider>("wikidata", (sp, _) =>
            sp.GetRequiredService<WikidataCatalogProvider>());
        services.AddKeyedScoped<ICatalogProvider>("bgg", (sp, _) =>
            sp.GetRequiredService<BggCatalogProvider>());

        services.AddScoped<ICatalogSeedAggregator>(sp =>
        {
            var wikidata = sp.GetRequiredKeyedService<ICatalogProvider>("wikidata");
            var bgg = sp.GetRequiredKeyedService<ICatalogProvider>("bgg");
            var logger = sp.GetRequiredService<ILogger<CatalogSeedAggregator>>();
            return new CatalogSeedAggregator(wikidata, bgg, logger);
        });
    }

    /// <summary>
    /// Issue #1823 Wave 3 M15 (ADR DEC-3i) — registers
    /// <see cref="WikidataQuarterlyReVerificationJob"/> with the Quartz
    /// scheduler. Runs at 04:00 UTC on the 1st day of every 3rd month
    /// (Jan / Apr / Jul / Oct). Idempotent re-runs on the same day touch
    /// zero rows.
    /// </summary>
    private static void RegisterWikidataQuarterlyReVerificationJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("WikidataQuarterlyReVerificationJob", "SharedGameCatalog");

            q.AddJob<WikidataQuarterlyReVerificationJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("WikidataQuarterlyReVerificationTrigger", "SharedGameCatalog")
                // 04:00 UTC on the 1st day of every 3rd month (Jan / Apr / Jul / Oct).
                // 1 hour later than the 03:00 retention sweep so the two jobs do
                // not contend over the same connection pool.
                .WithCronSchedule("0 0 4 1 */3 ?")
                .WithDescription("Issue #1823 Wave 3 ADR DEC-3i: resets WikidataQidLastVerifiedAt for SharedGames whose last verification is older than 90 days. Runs quarterly at 04:00 UTC."));
        });
    }

    /// <summary>
    /// Issue #1823 Wave 3 retention sweep (ADR DEC-3j) — registers
    /// <see cref="WikidataCoverDeadLetterRetentionJob"/> with the Quartz
    /// scheduler. Runs daily at 03:00 UTC. Idempotent: sweeping twice the same
    /// day yields zero additional deletions.
    /// </summary>
    private static void RegisterWikidataCoverDeadLetterRetentionJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("WikidataCoverDeadLetterRetentionJob", "SharedGameCatalog");

            q.AddJob<WikidataCoverDeadLetterRetentionJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("WikidataCoverDeadLetterRetentionTrigger", "SharedGameCatalog")
                .WithCronSchedule("0 0 3 * * ?")
                .WithDescription("Issue #1823 Wave 3 ADR DEC-3j: deletes wikidata_cover_enrichment_attempts rows whose dead_lettered_at is older than 7 days. Runs daily at 03:00 UTC."));
        });
    }

    /// <summary>
    /// Issue #1823 Wave 3 M9 — registers <see cref="WikidataCoverEnrichmentJob"/>
    /// with the Quartz scheduler. Runs every 1 minute (single-pod batch, HPA=1
    /// per DEC-3e). The job picks up to 30 games per tick that are due for
    /// enrichment, throttling 1s between items.
    /// </summary>
    private static void RegisterWikidataCoverEnrichmentJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("WikidataCoverEnrichmentJob", "SharedGameCatalog");

            q.AddJob<WikidataCoverEnrichmentJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("WikidataCoverEnrichmentTrigger", "SharedGameCatalog")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(1)
                    .RepeatForever())
                .WithDescription("Issue #1823 Wave 3 M9: drives the Wikidata cover enrichment pipeline every 1 minute. DEC-3j retry + dead-letter policy is applied per game."));
        });
    }

    /// <summary>
    /// Issue #1903 M5.2 — registers <see cref="CatalogSeedFetchJob"/> with the
    /// Quartz scheduler. Runs every 1 minute to fetch provider data for
    /// <c>Pending</c> drafts (batch size 10, 1s inter-item throttle per spec §7).
    /// </summary>
    private static void RegisterCatalogSeedFetchJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("CatalogSeedFetchJob", "SharedGameCatalog");

            q.AddJob<CatalogSeedFetchJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("CatalogSeedFetchTrigger", "SharedGameCatalog")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(1)
                    .RepeatForever())
                .WithDescription("Fetches provider data for Pending CatalogSeedDraft entries every 1 minute"));
        });
    }

    /// <summary>
    /// Issue #1903 M7.1 — registers <see cref="BggTosWatcherJob"/> with the
    /// Quartz scheduler. Runs every 30 days at the trigger's start time + a
    /// 1-minute kickoff after process start, so the first deploy primes the
    /// singleton row almost immediately. Pure HTTP GET against the BGG ToS
    /// URL; no rate-limit concerns at monthly cadence.
    /// </summary>
    private static void RegisterBggTosWatcherJob(IServiceCollection services)
    {
        // The job uses IHttpClientFactory.CreateClient() (default client) so we
        // don't need a typed AddHttpClient registration here. AddHttpClient is
        // already exposed via Program.cs / other contexts that register typed
        // clients, which transitively registers IHttpClientFactory itself.
        services.AddHttpClient();

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("BggTosWatcherJob", "SharedGameCatalog");

            q.AddJob<BggTosWatcherJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("BggTosWatcherTrigger", "SharedGameCatalog")
                // 30-day interval (~monthly). Quartz SimpleSchedule supports hours,
                // so 24 * 30 = 720 hours.
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(24 * 30)
                    .RepeatForever())
                // Kick off 1 minute after process start so the first deploy
                // primes the singleton row without delaying boot.
                .StartAt(DateBuilder.FutureDate(1, IntervalUnit.Minute))
                .WithDescription("Monthly BGG ToS hash watcher (issue #1903 M7.1, spec §8.5.6)"));
        });
    }

    /// <summary>
    /// Registers authorization policies for SharedGameCatalog endpoints.
    /// Issue #2371 Phase 2
    /// </summary>
    public static IServiceCollection AddSharedGameCatalogPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOrEditorPolicy", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Editor"))
            .AddPolicy("AdminOnlyPolicy", policy =>
                policy.RequireRole("SuperAdmin", "Admin"));

        return services;
    }
}
