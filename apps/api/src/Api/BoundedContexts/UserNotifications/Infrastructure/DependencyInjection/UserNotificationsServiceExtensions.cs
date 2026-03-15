using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;


namespace Api.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for UserNotifications bounded context.
/// Issue #2053: User notifications system.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal static class UserNotificationsServiceExtensions
{
    /// <summary>
    /// Registers all UserNotifications bounded context services.
    /// </summary>
    public static IServiceCollection AddUserNotificationsContext(this IServiceCollection services, IConfiguration configuration)
    {
        // Slack notification configuration binding
        services.Configure<SlackNotificationConfiguration>(
            configuration.GetSection(SlackNotificationConfiguration.SectionName));

        // Register repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>(); // Issue #4417
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>(); // Issue #52: Email template admin management
        services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>(); // Slack notification queue
        services.AddScoped<ISlackConnectionRepository, SlackConnectionRepository>(); // Slack connections

        // Slack signature validation
        services.AddSingleton<SlackSignatureValidator>();

        // Slack Block Kit message builders
        services.AddSingleton<GenericSlackBuilder>();
        services.AddSingleton<ISlackMessageBuilder, ShareRequestSlackBuilder>();
        services.AddSingleton<ISlackMessageBuilder, GameNightSlackBuilder>();
        services.AddSingleton<ISlackMessageBuilder, PdfProcessingSlackBuilder>();
        services.AddSingleton<ISlackMessageBuilder, BadgeSlackBuilder>();
        services.AddSingleton<ISlackMessageBuilder, AdminAlertSlackBuilder>();
        services.AddSingleton<SlackMessageBuilderFactory>();

        // Register services
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>(); // Multi-channel dispatch
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>(); // Issue #4417
        services.AddSingleton<IUserNotificationBroadcaster, InMemoryUserNotificationBroadcaster>(); // Issue #5005
        services.AddSingleton<IUnsubscribeTokenService, UnsubscribeTokenService>(); // Issue #38

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

        // ISSUE-2742: Quartz.NET configuration for cooldown reminder scheduling
        services.AddQuartz(q =>
        {
            // Register cooldown end reminder job (runs hourly)
            q.AddJob<CooldownEndReminderJob>(opts => opts
                .WithIdentity("cooldown-end-reminder-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("cooldown-end-reminder-job", "notifications")
                .WithIdentity("cooldown-end-reminder-trigger", "notifications")
                .WithCronSchedule("0 0 * * * ?")  // Every hour at minute 0
                .WithDescription("Runs hourly to notify users when their cooldown period ends"));
        });

        // ISSUE-4417: Email processor job - runs every 30 seconds
        services.AddQuartz(q =>
        {
            q.AddJob<EmailProcessorJob>(opts => opts
                .WithIdentity("email-processor-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("email-processor-job", "notifications")
                .WithIdentity("email-processor-trigger", "notifications")
                .WithCronSchedule("0/30 * * * * ?")  // Every 30 seconds
                .WithDescription("Processes queued emails with retry and dead letter handling"));
        });

        // Slack notification processor job - every 10 seconds
        services.AddQuartz(q =>
        {
            q.AddJob<SlackNotificationProcessorJob>(opts => opts
                .WithIdentity("slack-notification-processor-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("slack-notification-processor-job", "notifications")
                .WithIdentity("slack-notification-processor-trigger", "notifications")
                .WithCronSchedule("0/10 * * * * ?")
                .WithDescription("Processes queued Slack notifications with rate limiting"));
        });

        // ISSUE-40: Dead letter monitor job - hourly
        services.AddQuartz(q =>
        {
            q.AddJob<DeadLetterMonitorJob>(opts => opts
                .WithIdentity("dead-letter-monitor-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("dead-letter-monitor-job", "notifications")
                .WithIdentity("dead-letter-monitor-trigger", "notifications")
                .WithCronSchedule("0 0 * * * ?")  // Every hour at minute 0
                .WithDescription("Monitors dead letter queue and alerts admin when threshold exceeded"));
        });

        // ISSUE-41: Notification cleanup job - daily at 3 AM UTC
        services.AddQuartz(q =>
        {
            q.AddJob<NotificationCleanupJob>(opts => opts
                .WithIdentity("notification-cleanup-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("notification-cleanup-job", "notifications")
                .WithIdentity("notification-cleanup-trigger", "notifications")
                .WithCronSchedule("0 0 3 * * ?")  // Daily at 3:00 AM UTC
                .WithDescription("Cleans up read notifications and old email queue items past retention period"));
        });

        // Note: AddQuartzHostedService is called once in Administration context
        // No need to duplicate - jobs from all contexts share the same Quartz scheduler

        return services;
    }
}
