using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Api.Services;
using Api.Configuration;
using Api.Authentication;
using Api.BoundedContexts.Authentication.Application.Interfaces;
using Api.BoundedContexts.Authentication.Infrastructure;

namespace Api.Extensions;

public static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataProtectionServices();
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
        services.AddAuthorization();

        return services;
    }

    private static IServiceCollection AddDataProtectionServices(this IServiceCollection services)
    {
        // AUTH-06: Data Protection API for OAuth token encryption
        services.AddDataProtection();

        return services;
    }

    private static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        // CODE-QUALITY: Centralized password hashing service (PBKDF2-HMAC-SHA256)
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.AddSingleton<ApiKeyCookieService>();

        // Core authentication - MIGRATED TO DDD/CQRS
        // AuthService removed - all auth operations now use MediatR handlers in BoundedContexts.Authentication

        // API-01: API key authentication service
        services.AddScoped<ApiKeyAuthenticationService>();

        // API-04: API key management service

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
