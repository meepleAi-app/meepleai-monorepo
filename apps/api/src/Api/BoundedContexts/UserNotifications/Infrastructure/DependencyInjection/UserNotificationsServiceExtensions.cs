using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

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
        // Event handlers: ShareRequestCreated, Approved, Rejected, ChangesRequested (Issue #2739)
        // Event handler: NewShareRequestAdminAlert (Issue #2740)

        // ISSUE-2740: Quartz.NET configuration for admin notification jobs
        services.AddQuartz(q =>
        {
            // Admin digest job - daily at 9:00 AM UTC
            q.AddJob<AdminShareRequestDigestJob>(opts => opts
                .WithIdentity("admin-share-request-digest-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("admin-share-request-digest-job", "notifications")
                .WithIdentity("admin-digest-trigger", "notifications")
                .WithCronSchedule("0 0 9 * * ?")  // Daily at 9:00 AM UTC
                .WithDescription("Sends daily digest of pending share requests to admins"));

            // Stale request warning job - every 6 hours
            q.AddJob<StaleShareRequestWarningJob>(opts => opts
                .WithIdentity("stale-share-request-warning-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("stale-share-request-warning-job", "notifications")
                .WithIdentity("stale-warning-trigger", "notifications")
                .WithCronSchedule("0 0 */6 * * ?")  // Every 6 hours at minute 0
                .WithDescription("Warns admins about share requests pending longer than threshold"));
        });

        return services;
    }
}
