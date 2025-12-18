using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for UserNotifications bounded context.
/// Issue #2053: User notifications system.
/// </summary>
internal static class UserNotificationsServiceExtensions
{
    /// <summary>
    /// Registers all UserNotifications bounded context services.
    /// </summary>
    public static IServiceCollection AddUserNotificationsContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
