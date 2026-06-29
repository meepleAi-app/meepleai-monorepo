using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering SessionTracking bounded context services.
/// </summary>
internal static class SessionTrackingServiceExtensions
{
    /// <summary>
    /// Registers all SessionTracking bounded context services.
    /// </summary>
    public static IServiceCollection AddSessionTrackingContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IScoreEntryRepository, ScoreEntryRepository>();
        services.AddScoped<IDiceRollRepository, DiceRollRepository>();
        services.AddScoped<ISessionDeckRepository, SessionDeckRepository>();
        services.AddScoped<ISessionNoteRepository, SessionNoteRepository>();
        services.AddScoped<ISessionMediaRepository, SessionMediaRepository>(); // ISSUE-4760
        services.AddScoped<ISessionChatRepository, SessionChatRepository>(); // ISSUE-4760
        services.AddScoped<IToolkitSessionStateRepository, ToolkitSessionStateRepository>(); // ISSUE-5148: Epic B5
        services.AddScoped<ISessionEventRepository, SessionEventRepository>(); // ISSUE-276: Session Diary / Timeline
        services.AddScoped<ISessionCheckpointRepository, SessionCheckpointRepository>(); // ISSUE-278: Session Checkpoint / Deep Save
        services.AddScoped<IVisionSnapshotRepository, VisionSnapshotRepository>(); // Session Vision AI
        services.AddScoped<IGamebookCampaignSessionRepository, GamebookCampaignSessionRepository>(); // Iter 1.A — Libro Game gamebook campaigns
        services.AddScoped<IGamebookPhotoArtifactRepository, GamebookPhotoArtifactRepository>(); // Iter 1.B
        services.AddScoped<ITranslatedParagraphRepository, TranslatedParagraphRepository>(); // Iter 1.B
        services.AddScoped<IGamebookGlossaryRepository, GamebookGlossaryRepository>(); // Iter 1.B
        services.AddScoped<ISessionBookProgressRepository, SessionBookProgressRepository>(); // Task C1 — gamebook multi-book generalization

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain services
        services.AddScoped<ISessionQuotaService, SessionQuotaService>();

        // GST-003: Register SSE synchronization service (singleton for shared state)
        services.AddSingleton<ISessionSyncService, SessionSyncService>();

        // Issue #2561 SP2 T6: Monotonic per-session sequence provider for SSE event IDs
        // (Redis INCR + in-process fallback). Singleton — holds the in-process fallback counter dict.
        services.AddSingleton<ISessionSequenceProvider, RedisSessionSequenceProvider>();

        // Issue #4764: Enhanced broadcast service with Redis Pub/Sub, connection pooling,
        // event buffering, selective broadcasting, and Last-Event-ID reconnection
        services.AddSingleton<ISessionBroadcastService, SessionBroadcastService>();

        // Issue #3345: Register timer state manager (singleton for in-memory timer state)
        services.AddSingleton<TimerStateManager>();

        // Auto-save scheduler service (dynamic per-session Quartz jobs)
        services.AddScoped<IAutoSaveSchedulerService, QuartzAutoSaveSchedulerService>();

        // Issue #376: SSE diary stream (Channel-based in-process pub/sub)
        services.AddSingleton<IDiaryStreamService, DiaryStreamService>();

        // F3: AutoSave health observability — tracker, logger, and TimeProvider
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAutoSaveHealthTracker, AutoSaveHealthTracker>();
        services.AddHostedService<AutoSaveHealthLoggerService>();

        // Session Vision AI
        services.AddScoped<IGameStateExtractor, GameStateExtractor>();

        // Iter 1.B — Libro Game photo storage (EXIF strip adapter over IBlobStorageService)
        services.AddScoped<IGamebookPhotoStorage, GamebookPhotoStorageService>(); // Iter 1.B

        // Issue #1415 — Campaign ownership guard for SSE pre-flight + handler ownership checks
        services.AddScoped<ICampaignOwnershipGuard, CampaignOwnershipGuard>();

        // Iter 1.B — Tesseract OCR engine (singleton: engine is thread-safe, pages are per-call)
        services.AddSingleton<IOcrService, TesseractOcrService>(); // Iter 1.B

        // #1559: NLP lang detection (NTextCat heuristic, ~5-15ms per text).
        // Singleton: NTextCatLanguageDetectionService loads ~2.4MB profile once at startup
        // and is thread-safe (RankedLanguageIdentifier is read-only post-construction).
        services.AddSingleton<ILanguageDetectionService, NTextCatLanguageDetectionService>();

        // Asse A semantic alignment #1896 (T10, DEC-1): polymorphic scoring strategy factory.
        // Singleton — instantiates stateless strategies on demand (no internal state, thread-safe).
        // Consumed by UpdateSessionScoresCommandValidator + UpdateSessionScoresCommandHandler.
        services.AddSingleton<ScoringStrategyFactory>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
