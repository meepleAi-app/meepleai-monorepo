using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Api.Services;
using Api.Configuration;
using Api.Infrastructure.Authentication;
using Api.BoundedContexts.Authentication.Application.Interfaces;
using Api.BoundedContexts.Authentication.Infrastructure;

namespace Api.Extensions;

internal static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDataProtectionServices(configuration, environment);
        services.AddAuthServices();
        services.AddSessionServices();
        services.AddPasswordResetServices();
        services.AddRateLimitServices();
        services.AddAlertingServices(configuration);

        // TEST-650 (#659): Add ASP.NET Core Authentication with SessionAuthenticationHandler
        // This fixes 500 errors when .RequireAuthorization() is called on endpoints.
        //
        // Architecture:
        // - SessionAuthenticationMiddleware: Runs first, populates HttpContext.Items[ActiveSession]
        // - SessionAuthenticationHandler: Integrates with ASP.NET auth system, provides proper 401/403
        // - Both use the same AuthService.ValidateSessionAsync() for consistency
        //
        // Before: DefaultChallengeScheme = null → InvalidOperationException → 500 error
        // After: SessionAuthenticationHandler → proper 401 Unauthorized / 403 Forbidden
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "SessionCookie";
            options.DefaultChallengeScheme = "SessionCookie";
        })
        .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("SessionCookie", _ => { });

        // Add ASP.NET Core Authorization services (required for UseAuthorization middleware)
        // ISSUE-3690: Role-based authorization policies for admin dashboard
        services.AddAuthorization(options =>
        {
            // SuperAdmin only - full system access including global feature flags
            options.AddPolicy("RequireSuperAdmin", policy =>
                policy.RequireRole("SuperAdmin"));

            // Admin or above - operations, monitoring, user management
            options.AddPolicy("RequireAdminOrAbove", policy =>
                policy.RequireRole("SuperAdmin", "Admin"));

            // Editor or above - content management
            options.AddPolicy("RequireEditorOrAbove", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Editor"));
        });

        return services;
    }

    private static IServiceCollection AddDataProtectionServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // AUTH-06: Data Protection API for OAuth token encryption.
        // C4: same key ring is used for the meepleai_user_role_v2 cookie HMAC.
        // Without persisted keys, every API process restart invalidates every
        // role cookie in the wild — fine for local dev / unit tests, fatal in
        // production. Opt in to file-system persistence by setting
        // DataProtection:KeysPath; multi-instance deployments should point all
        // replicas at the same volume (or migrate to PersistKeysToStackExchangeRedis).
        var dp = services.AddDataProtection()
            .SetApplicationName("MeepleAi")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            Directory.CreateDirectory(keysPath);
            dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        else
        {
            // F4 (auth security review): persisted keys are mandatory in
            // Production / Staging. Without them every restart generates a
            // fresh key ring and invalidates every meepleai_user_role_v2
            // cookie in the wild — users silently lose admin role gating
            // until they re-login. The previous behaviour failed silently
            // (just no persistence). We now hard-fail at startup so
            // misconfigured deployments don't ship.
            //
            // Development / Testing / CI keep ephemeral keys (current
            // behaviour) so unit tests and local dev don't need a writable
            // path.
            if (environment.IsProduction() || environment.IsStaging())
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath is required in Production and Staging. " +
                    "Set it to a writable directory mounted on a persistent volume " +
                    "(env var: DATAPROTECTION__KEYSPATH). Without persisted keys, " +
                    "every API restart invalidates every meepleai_user_role_v2 " +
                    "cookie. Multi-instance deployments must point all replicas " +
                    "at the same volume (or use PersistKeysToStackExchangeRedis).");
            }
        }

        return services;
    }

    private static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        // CODE-QUALITY: Centralized password hashing service (PBKDF2-HMAC-SHA256)
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

        // Core authentication - MIGRATED TO DDD/CQRS
        // AuthService removed - all auth operations now use MediatR handlers in BoundedContexts.Authentication

        // AUTH-06: OAuth services
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IOAuthStateStore, RedisOAuthStateStore>(); // Redis-backed state storage for distributed deployments
        services.AddScoped<IOAuthService, OAuthService>();

        // AUTH-07: Two-factor authentication services
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ITempSessionService, TempSessionService>();

        return services;
    }

    private static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        // AUTH-03: Session management service
        services.AddHostedService<SessionAutoRevocationService>();

        return services;
    }

    private static IServiceCollection AddPasswordResetServices(this IServiceCollection services)
    {
        // AUTH-04: Password reset services
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IEmailService, EmailService>();

        // ISSUE-4416: Push notification service
        services.AddScoped<IPushNotificationService, PushNotificationService>();

        // ISSUE-3071: Email verification service
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();

        return services;
    }

    private static IServiceCollection AddRateLimitServices(this IServiceCollection services)
    {
        services.AddScoped<IRateLimitService, RateLimitService>();

        return services;
    }

    private static IServiceCollection AddAlertingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // OPS-07: Alerting system
        services.Configure<AlertingConfiguration>(configuration.GetSection("Alerting"));
        services.AddScoped<IAlertingService, AlertingService>();
        services.AddScoped<IAlertChannel, EmailAlertChannel>();
        services.AddScoped<IAlertChannel, SlackAlertChannel>();
        services.AddScoped<IAlertChannel, PagerDutyAlertChannel>();

        return services;
    }
}
