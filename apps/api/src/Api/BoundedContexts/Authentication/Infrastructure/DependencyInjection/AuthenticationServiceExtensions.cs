using Api.BoundedContexts.Authentication.Application.Configuration;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Authentication.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for Authentication bounded context.
/// </summary>
internal static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Registers all Authentication bounded context services.
    /// </summary>
    public static IServiceCollection AddAuthenticationContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>(); // ISSUE-2052
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>(); // ISSUE-124
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>(); // ISSUE-124: Access request management
        services.AddScoped<IWaitlistEntryRepository, WaitlistEntryRepository>(); // ISSUE-589: Public Alpha waitlist (Wave A.2)

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Admin Invitation Flow: singleton channel for game suggestion processing
        services.AddSingleton<GameSuggestionChannel>();

        // DevOps Wave 1 (#845): staging email allowlist guard.
        // Singleton: backed by IMemoryCache with 60s TTL + domain-event invalidation.
        // Reads from staging_allowlist DB table via IServiceScopeFactory on cache miss.
        // Used by StagingAccessMiddleware (active only when ASPNETCORE_ENVIRONMENT=Staging).
        services.AddMemoryCache();
        services.AddSingleton<IStagingAccessGuard, StagingAccessGuard>();

        // SP5 Admin Security S3 — D-S3-1: typed wrapper around the "TwoFactor:StrictMode"
        // dynamic config flag, consumed by TwoFactorEnforcementBehavior (T4) without per-test
        // IConfigurationService setup. Scoped because IConfigurationService is scoped.
        services.AddScoped<ITwoFactorEnforcementConfiguration, TwoFactorEnforcementConfiguration>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
