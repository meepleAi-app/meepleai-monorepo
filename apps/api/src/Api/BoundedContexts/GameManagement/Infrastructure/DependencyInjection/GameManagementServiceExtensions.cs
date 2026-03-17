using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Infrastructure.Scheduling;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Api.BoundedContexts.GameManagement.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for GameManagement bounded context.
/// </summary>
internal static class GameManagementServiceExtensions
{
    /// <summary>
    /// Registers all GameManagement bounded context services.
    /// </summary>
    public static IServiceCollection AddGameManagementContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IGameSessionStateRepository, GameSessionStateRepository>(); // ISSUE-2403
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>(); // ISSUE-3889
        services.AddScoped<IRuleConflictFaqRepository, RuleConflictFaqRepository>(); // ISSUE-3761: Conflict FAQ
        services.AddSingleton<ILiveSessionRepository, LiveSessionRepository>(); // Issue #4749: Live session in-memory store
        services.AddScoped<IToolStateRepository, ToolStateRepository>(); // Issue #4754: ToolState persistence
        services.AddScoped<ISessionSnapshotRepository, SessionSnapshotRepository>(); // Issue #4755: SessionSnapshot persistence
        services.AddScoped<IPauseSnapshotRepository, PauseSnapshotRepository>(); // Game Night: full-state pause snapshots
        services.AddScoped<IGameReviewRepository, GameReviewRepository>();
        services.AddScoped<IGameStrategyRepository, GameStrategyRepository>();
        services.AddScoped<ITurnOrderRepository, TurnOrderRepository>(); // Issue #4970: TurnOrder base toolkit
        services.AddScoped<IWhiteboardStateRepository, WhiteboardStateRepository>(); // Issue #4971: Whiteboard base toolkit
        services.AddScoped<ISessionAttachmentRepository, SessionAttachmentRepository>(); // Issue #5360: Session photo attachments
        services.AddScoped<IGameNightPlaylistRepository, GameNightPlaylistRepository>(); // Issue #5582: Game Night Playlist
        services.AddScoped<IGameNightEventRepository, GameNightEventRepository>(); // Issue #42: Game Night Event

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain services (Issue #1185, #869)
        services.AddScoped<RuleSpecVersioningDomainService>();
        services.AddScoped<RuleAtomParsingDomainService>();
        services.AddScoped<RuleSpecDiffDomainService>();
        services.AddScoped<MoveValidationDomainService>();

        // Issue #2055: Register collaborative editing services
        services.AddScoped<IEditorLockService, EditorLockService>();

        // Issue #3070: Register session quota service
        services.AddScoped<ISessionQuotaService, SessionQuotaService>();

        // Issue #3891: Register play record permission checker
        services.AddScoped<PlayRecordPermissionChecker>();

        // Issue #5361: Session attachment service (upload, thumbnail, S3)
        services.AddScoped<ISessionAttachmentService, SessionAttachmentService>();

        // Issue #5579: GameSessionContext cross-context orchestrator
        services.AddScoped<IGameSessionOrchestratorService, GameSessionOrchestratorService>();

        // Game Night AI Assistant: Player name resolution for score parsing
        services.AddScoped<IPlayerNameResolutionService, PlayerNameResolutionService>();

        // Issue #44/#47: Game night email templates
        services.AddScoped<IGameNightEmailService, GameNightEmailService>();

        // Issue #5366: Session attachment cleanup background job
        services.AddHostedService<SessionAttachmentCleanupJob>();

        // Issue #45: Game Night Reminder job - runs every 15 minutes
        services.AddQuartz(q =>
        {
            q.AddJob<GameNightReminderJob>(opts => opts
                .WithIdentity("game-night-reminder-job", "game-management")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("game-night-reminder-job", "game-management")
                .WithIdentity("game-night-reminder-trigger", "game-management")
                .WithCronSchedule("0 0/15 * * * ?")  // Every 15 minutes
                .WithDescription("Sends 24h and 1h reminders for upcoming game nights"));
        });

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
