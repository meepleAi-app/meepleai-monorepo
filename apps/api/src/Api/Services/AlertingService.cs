using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Service for managing system alerts from Prometheus AlertManager.
/// OPS-07: Alerting system for critical errors and anomalies.
///
/// Features:
/// - Multi-channel notifications (Email, Slack, PagerDuty)
/// - Alert throttling (1 alert per hour max for same type)
/// - Auto-resolution when metrics return to normal
/// - Alert history tracking
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEnumerable<IAlertChannel> _alertChannels;
    private readonly AlertingConfiguration _config;
    private readonly ILogger<AlertingService> _logger;
    private readonly TimeProvider _timeProvider;

    public AlertingService(
        MeepleAiDbContext dbContext,
        IEnumerable<IAlertChannel> alertChannels,
        IOptions<AlertingConfiguration> config,
        ILogger<AlertingService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _alertChannels = alertChannels;
        _config = config.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AlertDto> SendAlertAsync(
        string alertType,
        string severity,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("Alerting is disabled. Alert not sent: {AlertType}", alertType);
            throw new InvalidOperationException("Alerting system is disabled");
        }

        // Check throttling
        if (await IsThrottledAsync(alertType, cancellationToken))
        {
            _logger.LogInformation(
                "Alert {AlertType} is throttled. Skipping send (throttle window: {ThrottleMinutes} minutes)",
                alertType,
                _config.ThrottleMinutes);

            // Return most recent alert instead
            var existingAlert = await _dbContext.Alerts
                .AsNoTracking()
                .Where(a => a.AlertType == alertType && a.IsActive)
                .OrderByDescending(a => a.TriggeredAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingAlert != null)
            {
                return MapToDto(existingAlert);
            }

            throw new InvalidOperationException($"Alert {alertType} is currently throttled");
        }

        // Create alert entity
        var alertEntity = new AlertEntity
        {
            AlertType = alertType,
            Severity = severity,
            Message = message,
            Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
            TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime,
            IsActive = true
        };

        // Send to all enabled channels
        var channelResults = new Dictionary<string, bool>();
        foreach (var channel in _alertChannels)
        {
            try
            {
                var success = await channel.SendAsync(
                    alertType,
                    severity,
                    message,
                    metadata,
                    cancellationToken);

                channelResults[channel.ChannelName] = success;

                if (success)
                {
                    _logger.LogInformation(
                        "Alert {AlertType} sent successfully via {Channel}",
                        alertType,
                        channel.ChannelName);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send alert {AlertType} via {Channel}",
                        alertType,
                        channel.ChannelName);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Background service boundary - alert channel failure isolation
            // RESILIENCE PATTERN: Alert channel failures must not stop other alert channels
            // Rationale: Multi-channel alerting requires fault isolation - if email fails,
            // Slack/PagerDuty should still deliver. We track per-channel results and log
            // failures for monitoring. This enables graceful degradation when channels fail.
            // Context: Channel failures are typically external (SMTP down, Slack API timeout)
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending alert {AlertType} via {Channel}",
                    alertType,
                    channel.ChannelName);
                channelResults[channel.ChannelName] = false;
            }
#pragma warning restore CA1031
        }

        alertEntity.ChannelSent = JsonSerializer.Serialize(channelResults);

        // Save to database
        _dbContext.Alerts.Add(alertEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Alert {AlertType} created with severity {Severity}. Channels: {Channels}",
            alertType,
            severity,
            string.Join(", ", channelResults.Where(c => c.Value).Select(c => c.Key)));

        return MapToDto(alertEntity);
    }

    public async Task<bool> ResolveAlertAsync(
        string alertType,
        CancellationToken cancellationToken = default)
    {
        var activeAlerts = await _dbContext.Alerts
            .Where(a => a.AlertType == alertType && a.IsActive)
            .ToListAsync(cancellationToken);

        if (activeAlerts.Count == 0)
        {
            _logger.LogInformation("No active alerts found for type {AlertType}", alertType);
            return false;
        }

        foreach (var alert in activeAlerts)
        {
            alert.IsActive = false;
            alert.ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Resolved {Count} alert(s) for type {AlertType}",
            activeAlerts.Count,
            alertType);

        return true;
    }

    public async Task<List<AlertDto>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken);

        return alerts.Select(MapToDto).ToList();
    }

    public async Task<List<AlertDto>> GetAlertHistoryAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.TriggeredAt >= fromDate && a.TriggeredAt <= toDate)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken);

        return alerts.Select(MapToDto).ToList();
    }

    public async Task<bool> IsThrottledAsync(
        string alertType,
        CancellationToken cancellationToken = default)
    {
        var throttleWindow = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-_config.ThrottleMinutes);

        var recentAlert = await _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.AlertType == alertType && a.TriggeredAt >= throttleWindow)
            .AnyAsync(cancellationToken);

        return recentAlert;
    }

    private static AlertDto MapToDto(AlertEntity entity)
    {
        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata);
        }

        Dictionary<string, bool>? channelSent = null;
        if (!string.IsNullOrEmpty(entity.ChannelSent))
        {
            channelSent = JsonSerializer.Deserialize<Dictionary<string, bool>>(entity.ChannelSent);
        }

        return new AlertDto(
            entity.Id,
            entity.AlertType,
            entity.Severity,
            entity.Message,
            metadata,
            entity.TriggeredAt,
            entity.ResolvedAt,
            entity.IsActive,
            channelSent
        );
    }
}

/// <summary>
/// Configuration for the alerting system.
/// Bind from appsettings.json:Alerting section.
/// </summary>
public class AlertingConfiguration
{
    public bool Enabled { get; set; } = true;
    public int ThrottleMinutes { get; set; } = 60;
    public EmailConfiguration Email { get; set; } = new();
    public SlackConfiguration Slack { get; set; } = new();
    public PagerDutyConfiguration PagerDuty { get; set; } = new();
}

public class EmailConfiguration
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public bool UseTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class SlackConfiguration
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "#alerts";
}

public class PagerDutyConfiguration
{
    public bool Enabled { get; set; }
    public string IntegrationKey { get; set; } = string.Empty;
}
