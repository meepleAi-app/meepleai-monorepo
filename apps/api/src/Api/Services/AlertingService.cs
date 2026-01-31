using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
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
internal class AlertingService : IAlertingService
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
        IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("Alerting is disabled. Alert not sent: {AlertType}", alertType);
            throw new InvalidOperationException("Alerting system is disabled");
        }

        // Check throttling
        var throttledResult = await HandleThrottledAlertAsync(alertType, cancellationToken).ConfigureAwait(false);
        if (throttledResult != null)
        {
            return throttledResult;
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
        var channelResults = await SendToChannelsAsync(alertType, severity, message, metadata, cancellationToken).ConfigureAwait(false);
        alertEntity.ChannelSent = JsonSerializer.Serialize(channelResults);

        // Save to database
        _dbContext.Alerts.Add(alertEntity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Alert {AlertType} created with severity {Severity}. Channels: {Channels}",
            alertType,
            severity,
            string.Join(", ", channelResults.Where(c => c.Value).Select(c => c.Key)));

        return MapToDto(alertEntity);
    }

    private async Task<AlertDto?> HandleThrottledAlertAsync(string alertType, CancellationToken cancellationToken)
    {
        if (!await IsThrottledAsync(alertType, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        _logger.LogInformation(
            "Alert {AlertType} is throttled. Skipping send (throttle window: {ThrottleMinutes} minutes)",
            alertType,
            _config.ThrottleMinutes);

        var existingAlert = await _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.AlertType == alertType && a.IsActive)
            .OrderByDescending(a => a.TriggeredAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (existingAlert != null)
        {
            return MapToDto(existingAlert);
        }

        throw new InvalidOperationException($"Alert {alertType} is currently throttled");
    }

    private async Task<Dictionary<string, bool>> SendToChannelsAsync(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata,
        CancellationToken cancellationToken)
    {
        var channelResults = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var channel in _alertChannels)
        {
            try
            {
                var success = await channel.SendAsync(alertType, severity, message, metadata, cancellationToken).ConfigureAwait(false);
                channelResults[channel.ChannelName] = success;
                LogChannelResult(alertType, channel.ChannelName, success);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // FAIL-OPEN PATTERN: Alert channel failures must not stop other alert channels
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending alert {AlertType} via {Channel}", alertType, channel.ChannelName);
                channelResults[channel.ChannelName] = false;
            }
#pragma warning restore CA1031
        }

        return channelResults;
    }

    private void LogChannelResult(string alertType, string channelName, bool success)
    {
        if (success)
        {
            _logger.LogInformation("Alert {AlertType} sent successfully via {Channel}", alertType, channelName);
        }
        else
        {
            _logger.LogWarning("Failed to send alert {AlertType} via {Channel}", alertType, channelName);
        }
    }

    public async Task<bool> ResolveAlertAsync(
        string alertType,
        CancellationToken cancellationToken = default)
    {
        var activeAlerts = await _dbContext.Alerts
            .Where(a => a.AlertType == alertType && a.IsActive)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

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

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            .ToListAsync(cancellationToken).ConfigureAwait(false);

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
            .ToListAsync(cancellationToken).ConfigureAwait(false);

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
            .AnyAsync(cancellationToken).ConfigureAwait(false);

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
internal class AlertingConfiguration
{
    public bool Enabled { get; set; } = true;
    public int ThrottleMinutes { get; set; } = 60;
    public EmailConfiguration Email { get; set; } = new();
    public SlackConfiguration Slack { get; set; } = new();
    public PagerDutyConfiguration PagerDuty { get; set; } = new();
}

internal class EmailConfiguration
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string From { get; set; } = string.Empty;
    public IList<string> To { get; set; } = new List<string>();
    public bool UseTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

internal class SlackConfiguration
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "#alerts";
}

internal class PagerDutyConfiguration
{
    public bool Enabled { get; set; }
    public string IntegrationKey { get; set; } = string.Empty;
}
