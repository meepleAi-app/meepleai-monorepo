using Api.Infrastructure;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Background service that periodically revokes inactive sessions.
/// Runs as an ASP.NET Core hosted service.
/// </summary>
internal class SessionAutoRevocationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionAutoRevocationService> _logger;
    private readonly SessionManagementConfiguration _config;

    private readonly TimeProvider _timeProvider;

    public SessionAutoRevocationService(
        IServiceScopeFactory scopeFactory,
        IOptions<SessionManagementConfiguration> config,
        ILogger<SessionAutoRevocationService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate configuration
        if (_config.AutoRevocationIntervalHours <= 0)
        {
            _logger.LogWarning(
                "Auto-revocation interval is {Hours} hours (invalid). Auto-revocation is disabled.",
                _config.AutoRevocationIntervalHours);
            return;
        }

        if (_config.InactivityTimeoutDays <= 0)
        {
            _logger.LogWarning(
                "Inactivity timeout is {Days} days (invalid). Auto-revocation is disabled.",
                _config.InactivityTimeoutDays);
            return;
        }

        _logger.LogInformation(
            "Session auto-revocation service started. Will run every {Hours} hours, timeout: {Days} days",
            _config.AutoRevocationIntervalHours,
            _config.InactivityTimeoutDays);

        // Wait a bit before the first run to allow the application to fully start
        await Task.Delay(TimeSpan.FromMinutes(1), _timeProvider, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RevokeInactiveSessionsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during session auto-revocation");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation during session auto-revocation");
            }

            // Wait for the configured interval before next run
            var delay = TimeSpan.FromHours(_config.AutoRevocationIntervalHours);
            _logger.LogDebug("Next auto-revocation check in {Hours} hours", _config.AutoRevocationIntervalHours);
            await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Session auto-revocation service stopped");
    }

    private async Task RevokeInactiveSessionsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running scheduled session auto-revocation check");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RevokeInactiveSessionsCommand();
            var revokedCount = await mediator.Send(command, ct).ConfigureAwait(false);

            if (revokedCount > 0)
            {
                _logger.LogInformation("Auto-revoked {Count} inactive sessions", revokedCount);
            }
            else
            {
                _logger.LogDebug("No inactive sessions to revoke");
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error revoking expired sessions");
            throw new InvalidOperationException("Failed to revoke expired sessions due to database error", ex);
        }
    }
}
