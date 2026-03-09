using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5477: Background service that monitors Redis connectivity for the rate-limiting subsystem.
/// Pings Redis every 30s and publishes InfrastructureDegradedEvent on failure.
/// Sends admin notifications on state transitions (healthy → degraded, degraded → recovered).
///
/// Configuration:
///   RedisHealthMonitor:CheckIntervalSeconds — polling interval (default: 30)
///   RedisHealthMonitor:FailureMode           — "soft" (warn, default) or "hard" (block OpenRouter)
/// </summary>
internal sealed class RedisRateLimitingHealthMonitor : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisRateLimitingHealthMonitor> _logger;

    private bool _wasHealthy = true;
    private int _consecutiveFailures;

    // Cooldown between admin notifications (avoid spam)
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(10);
    private DateTime _lastNotificationAt = DateTime.MinValue;

    internal TimeSpan CheckInterval =>
        TimeSpan.FromSeconds(_configuration.GetValue("RedisHealthMonitor:CheckIntervalSeconds", 30));

    internal string FailureMode =>
        _configuration.GetValue("RedisHealthMonitor:FailureMode", "soft") ?? "soft";

    /// <summary>
    /// Exposed for HybridLlmService to check if rate limiting is degraded.
    /// Thread-safe: reads/writes are atomic for booleans.
    /// </summary>
    internal bool IsRateLimitingDegraded { get; private set; }

    public RedisRateLimitingHealthMonitor(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RedisRateLimitingHealthMonitor> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RedisRateLimitingHealthMonitor starting — interval={Interval}s, failureMode={Mode}",
            CheckInterval.TotalSeconds, FailureMode);

        // Initial delay to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRedisHealthAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "RedisRateLimitingHealthMonitor: unexpected error during health check");
            }
#pragma warning restore CA1031

            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("RedisRateLimitingHealthMonitor stopped.");
    }

    private async Task CheckRedisHealthAsync(CancellationToken ct)
    {
        bool isHealthy;

        try
        {
            var db = _redis.GetDatabase();
            var pong = await db.PingAsync().ConfigureAwait(false);
            isHealthy = pong.TotalMilliseconds < 5000; // 5s timeout = degraded
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis PING failed — rate limiting subsystem degraded");
            isHealthy = false;
        }
#pragma warning restore CA1031

        if (isHealthy)
        {
            _consecutiveFailures = 0;

            if (!_wasHealthy)
            {
                // Transition: degraded → recovered
                IsRateLimitingDegraded = false;
                _wasHealthy = true;

                _logger.LogWarning("Redis rate-limiting subsystem recovered");

                await PublishEventAsync("Redis", "RateLimiting",
                    "Redis connectivity restored — rate limiting re-enabled",
                    isRecovered: true, ct).ConfigureAwait(false);

                await SendAdminNotificationAsync(
                    "Redis Rate Limiting Recovered",
                    "Redis connectivity has been restored. Rate limiting is active again.",
                    NotificationSeverity.Info, ct).ConfigureAwait(false);
            }
        }
        else
        {
            _consecutiveFailures++;

            if (_wasHealthy)
            {
                // Transition: healthy → degraded
                IsRateLimitingDegraded = true;
                _wasHealthy = false;

                _logger.LogError(
                    "Redis rate-limiting subsystem DEGRADED — rate limiting is disabled (failureMode={Mode})",
                    FailureMode);

                await PublishEventAsync("Redis", "RateLimiting",
                    $"Redis unavailable — rate limiting disabled (mode: {FailureMode})",
                    isRecovered: false, ct).ConfigureAwait(false);

                await SendAdminNotificationAsync(
                    "Rate Limiting Disabled — Redis Unavailable",
                    $"Redis is unreachable. Rate limiting is currently disabled (failure mode: {FailureMode}). " +
                    "OpenRouter requests are not being rate-limited. Monitor billing closely.",
                    NotificationSeverity.Error, ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug(
                    "Redis still unavailable — consecutive failures: {Count}",
                    _consecutiveFailures);
            }
        }
    }

    private async Task PublishEventAsync(
        string component, string subsystem, string message, bool isRecovered, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Publish(new InfrastructureDegradedEvent(
                component, subsystem, message, isRecovered, DateTime.UtcNow), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish InfrastructureDegradedEvent");
        }
#pragma warning restore CA1031
    }

    private async Task SendAdminNotificationAsync(
        string title, string message, NotificationSeverity severity, CancellationToken ct)
    {
        // Cooldown check
        if (DateTime.UtcNow - _lastNotificationAt < NotificationCooldown)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

            var adminIds = await dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (adminIds.Count == 0) return;

            var metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                component = "Redis",
                subsystem = "RateLimiting",
                failureMode = FailureMode,
                consecutiveFailures = _consecutiveFailures,
                alertedAt = DateTime.UtcNow
            });

            foreach (var adminId in adminIds)
            {
                var n = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminRedisRateLimitingDegraded,
                    severity: severity,
                    title: title,
                    message: message,
                    link: "/admin/agents/usage",
                    metadata: metadata);

                await notificationRepo.AddAsync(n, ct).ConfigureAwait(false);
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            _lastNotificationAt = DateTime.UtcNow;

            _logger.LogWarning(
                "Redis rate-limiting alert sent to {Count} admins: {Title}",
                adminIds.Count, title);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for Redis rate-limiting degradation");
        }
#pragma warning restore CA1031
    }
}
