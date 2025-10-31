using Microsoft.AspNetCore.DataProtection;
using Api.Services;
using Api.Configuration;

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

        // Add ASP.NET Core Authentication services (required for UseAuthentication middleware)
        // This registers IAuthenticationSchemeProvider and other authentication services
        // Note: We use a default authentication scheme but session authentication is handled
        // by custom SessionAuthenticationMiddleware which populates HttpContext.Items
        services.AddAuthentication(options =>
        {
            // No default scheme - authentication is handled by SessionAuthenticationMiddleware
            // and ApiKeyAuthenticationMiddleware via custom middleware, not ASP.NET Core schemes
            options.DefaultScheme = null;
            options.DefaultChallengeScheme = null;
        });

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
        // Core authentication
        services.AddScoped<AuthService>();

        // API-01: API key authentication service
        services.AddScoped<ApiKeyAuthenticationService>();

        // API-04: API key management service
        services.AddScoped<ApiKeyManagementService>();

        // AUTH-06: OAuth services
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IOAuthService, OAuthService>();

        // AUTH-07: Two-factor authentication services
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ITempSessionService, TempSessionService>();

        return services;
    }

    private static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        // AUTH-03: Session management service
        services.AddScoped<ISessionManagementService, SessionManagementService>();
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
