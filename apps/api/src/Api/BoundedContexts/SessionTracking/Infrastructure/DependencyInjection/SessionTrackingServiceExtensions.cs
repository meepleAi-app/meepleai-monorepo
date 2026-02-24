using Api.BoundedContexts.SessionTracking.Application.Handlers;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain services
        services.AddScoped<ISessionQuotaService, SessionQuotaService>();

        // GST-003: Register SSE synchronization service (singleton for shared state)
        services.AddSingleton<ISessionSyncService, SessionSyncService>();

        // Issue #4764: Enhanced broadcast service with Redis Pub/Sub, connection pooling,
        // event buffering, selective broadcasting, and Last-Event-ID reconnection
        services.AddSingleton<ISessionBroadcastService, SessionBroadcastService>();

        // Issue #3345: Register timer state manager (singleton for in-memory timer state)
        services.AddSingleton<TimerStateManager>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
