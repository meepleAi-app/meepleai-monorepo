using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Email;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
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

        // Email message builders (issue #3026). MVP ships only the generic fallback; register per-type
        // IEmailMessageBuilder implementations here later — the factory resolves them automatically.
        services.AddSingleton<GenericEmailBuilder>();
        services.AddSingleton<EmailMessageBuilderFactory>();

        AddSlackApiClient(services);

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

        // Issue #3026: Email notification processor job - every 30 seconds.
        // Drains channel_type=email items from notification_queue_items (previously orphaned — nothing
        // consumed that channel). 30s cadence matches the sibling email-delivery EmailProcessorJob:
        // email is not latency-critical, and a slower tick keeps SMTP throughput/backoff sane.
        services.AddQuartz(q =>
        {
            q.AddJob<EmailNotificationProcessorJob>(opts => opts
                .WithIdentity("email-notification-processor-job", "notifications")
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob("email-notification-processor-job", "notifications")
                .WithIdentity("email-notification-processor-trigger", "notifications")
                .WithCronSchedule("0/30 * * * * ?")  // Every 30 seconds
                .WithDescription("Processes queued email notifications (notification_queue_items, channel=email)"));
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

    /// <summary>
    /// Named HttpClient for the Slack API with a 10s timeout and a circuit breaker (opens after 5
    /// consecutive 5xx/timeout errors, stays open 2 minutes).
    /// <para>
    /// #3495 Slice E: the target URL comes from a stored Slack connection (webhook / API URL), so this
    /// is user-influenced external egress and dials through the SSRF connect-pin. The pin is the
    /// primary handler (innermost) and re-validates every auto-redirect hop; the circuit breaker sits
    /// outside it and keeps its own per-sink state.
    /// </para>
    /// </summary>
    private static void AddSlackApiClient(IServiceCollection services) =>
        services.AddHttpClient("SlackApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(2)))
        .ConfigureSsrfPin(MeepleAiMetrics.EgressSinks.Slack);

    /// <summary>
    /// Test seam (#3495 Slice E): registers ONLY the Slack API named client so a DI-resolution test
    /// can prove a Slack host resolving to a private address fails closed at the connect-pin. The
    /// caller MUST register an <see cref="Api.SharedKernel.Infrastructure.Http.IDnsResolver"/> on the
    /// same collection BEFORE calling this (the pin's own TryAdd default would otherwise win).
    /// </summary>
    internal static void AddSlackApiClientForTests(IServiceCollection services)
    {
        services.AddLogging();
        AddSlackApiClient(services);
    }
}
