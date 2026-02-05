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

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain services
        services.AddScoped<ISessionQuotaService, SessionQuotaService>();

        // GST-003: Register SSE synchronization service (singleton for shared state)
        services.AddSingleton<ISessionSyncService, SessionSyncService>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
