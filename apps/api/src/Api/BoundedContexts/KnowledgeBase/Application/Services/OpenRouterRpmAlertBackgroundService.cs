using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-5084: Background service that monitors OpenRouter RPM utilization
/// and sends in-app admin notifications when approaching the configured threshold.
///
/// Configuration (appsettings / env):
///   OpenRouterAlerts:RpmThresholdPercent  — utilization % that triggers an alert (default: 80)
///   OpenRouterAlerts:CooldownMinutes      — minimum minutes between alerts (default: 5)
///   OpenRouterAlerts:CheckIntervalSeconds — polling interval (default: 30)
/// </summary>
internal sealed class OpenRouterRpmAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOpenRouterRateLimitTracker _rateLimitTracker;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterRpmAlertBackgroundService> _logger;

    private const string Provider = "openrouter";

    // In-memory cooldown tracking (per severity level)
    private DateTime _lastAlertSentAt = DateTime.MinValue;

    private double ThresholdPercent =>
        _configuration.GetValue("OpenRouterAlerts:RpmThresholdPercent", 80.0);

    private TimeSpan Cooldown =>
        TimeSpan.FromMinutes(_configuration.GetValue("OpenRouterAlerts:CooldownMinutes", 5));

    private TimeSpan CheckInterval =>
        TimeSpan.FromSeconds(_configuration.GetValue("OpenRouterAlerts:CheckIntervalSeconds", 30));

    public OpenRouterRpmAlertBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOpenRouterRateLimitTracker rateLimitTracker,
        IConfiguration configuration,
        ILogger<OpenRouterRpmAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory
            ?? throw new ArgumentNullException(nameof(scopeFactory));
        _rateLimitTracker = rateLimitTracker
            ?? throw new ArgumentNullException(nameof(rateLimitTracker));
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OpenRouterRpmAlertBackgroundService starting — threshold={Threshold}%, cooldown={Cooldown}min",
            ThresholdPercent, Cooldown.TotalMinutes);

        // Initial delay to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRpmAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "OpenRouterRpmAlertBackgroundService: error during RPM check");
            }
#pragma warning restore CA1031

            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("OpenRouterRpmAlertBackgroundService stopped.");
    }

    private async Task CheckRpmAsync(CancellationToken cancellationToken)
    {
        var status = await _rateLimitTracker
            .GetCurrentStatusAsync(Provider, cancellationToken)
            .ConfigureAwait(false);

        // No limit configured — skip alerting
        if (status.LimitRpm == 0)
            return;

        var utilisationPercent = status.UtilizationPercent * 100.0;

        _logger.LogDebug(
            "RPM check: {Current}/{Limit} ({Util:F0}%)",
            status.CurrentRpm, status.LimitRpm, utilisationPercent);

        if (utilisationPercent < ThresholdPercent)
            return;

        // Enforce cooldown to avoid notification spam
        if (DateTime.UtcNow - _lastAlertSentAt < Cooldown)
            return;

        var message = $"Warning: OpenRouter RPM at {status.CurrentRpm}/{status.LimitRpm} " +
                      $"({utilisationPercent:F0}%). Consider enabling Ollama fallback.";

        await SendAdminNotificationsAsync(
            message, utilisationPercent, status.CurrentRpm, status.LimitRpm, cancellationToken)
            .ConfigureAwait(false);

        _lastAlertSentAt = DateTime.UtcNow;
    }

    private async Task SendAdminNotificationsAsync(
        string message,
        double utilisationPercent,
        int currentRpm,
        int limitRpm,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var adminIds = await dbContext.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => u.Role == "admin")
            .Select(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (adminIds.Count == 0)
        {
            _logger.LogDebug("OpenRouterRpmAlert: no admin users to notify");
            return;
        }

        var severity = utilisationPercent >= 95
            ? NotificationSeverity.Error
            : NotificationSeverity.Warning;

        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            currentRpm,
            limitRpm,
            utilisationPercent = Math.Round(utilisationPercent, 1),
            provider = Provider,
            alertedAt = DateTime.UtcNow
        });

        foreach (var adminId in adminIds)
        {
            var n = new Notification(
                id: Guid.NewGuid(),
                userId: adminId,
                type: NotificationType.AdminOpenRouterRpmAlert,
                severity: severity,
                title: $"OpenRouter RPM alert — {utilisationPercent:F0}% of limit used",
                message: message,
                link: "/admin/agents/usage",
                metadata: metadata);

            await notificationRepo.AddAsync(n, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "OpenRouter RPM alert sent to {Count} admins: {Current}/{Limit} ({Util:F0}%)",
            adminIds.Count, currentRpm, limitRpm, utilisationPercent);
    }
}
