using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
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
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyUsageLogRepository, ApiKeyUsageLogRepository>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>(); // ISSUE-2052
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>(); // ISSUE-124
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>(); // ISSUE-124: Access request management

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
