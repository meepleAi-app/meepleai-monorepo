using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IGameStrategyRepository, GameStrategyRepository>(); // ISSUE-4903: Game strategies
        services.AddScoped<IGameReviewRepository, GameReviewRepository>(); // ISSUE-4904: Game reviews
        services.AddSingleton<ILiveSessionRepository, LiveSessionRepository>(); // Issue #4749: Live session in-memory store
        services.AddScoped<IToolStateRepository, ToolStateRepository>(); // Issue #4754: ToolState persistence
        services.AddScoped<ISessionSnapshotRepository, SessionSnapshotRepository>(); // Issue #4755: SessionSnapshot persistence

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

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
