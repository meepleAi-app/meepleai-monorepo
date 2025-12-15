using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for LogoutAllDevicesCommand.
/// Revokes all sessions for a user with optional current session exclusion and password verification.
/// </summary>
internal class LogoutAllDevicesCommandHandler : ICommandHandler<LogoutAllDevicesCommand, LogoutAllDevicesResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionCacheService? _sessionCache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LogoutAllDevicesCommandHandler> _logger;

    public LogoutAllDevicesCommandHandler(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<LogoutAllDevicesCommandHandler> logger,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LogoutAllDevicesResult> Handle(LogoutAllDevicesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify password if provided (optional security check)
        var passwordVerification = await VerifyPasswordIfProvidedAsync(request, cancellationToken).ConfigureAwait(false);
        if (passwordVerification != null)
        {
            return passwordVerification;
        }

        // Get and filter sessions to revoke
        var (sessionsToRevoke, currentSessionExcluded) = await GetSessionsToRevokeAsync(request, cancellationToken).ConfigureAwait(false);

        if (sessionsToRevoke.Count == 0)
        {
            _logger.LogInformation("No sessions to revoke for user {UserId}", request.UserId);
            return new LogoutAllDevicesResult(true, 0, false);
        }

        // Revoke sessions
        var tokenHashesToInvalidate = await RevokeSessionsAsync(sessionsToRevoke, cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        await InvalidateCacheAsync(tokenHashesToInvalidate, cancellationToken).ConfigureAwait(false);

        var currentSessionRevoked = DetermineIfCurrentSessionRevoked(request, tokenHashesToInvalidate);
        LogResult(request.UserId, tokenHashesToInvalidate.Count, currentSessionExcluded, currentSessionRevoked);

        return new LogoutAllDevicesResult(true, tokenHashesToInvalidate.Count, currentSessionRevoked);
    }

    private async Task<LogoutAllDevicesResult?> VerifyPasswordIfProvidedAsync(
        LogoutAllDevicesCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for logout-all-devices", request.UserId);
            return new LogoutAllDevicesResult(false, 0, false, "User not found");
        }

        if (!user.VerifyPassword(request.Password))
        {
            _logger.LogWarning("Invalid password provided for logout-all-devices by user {UserId}", request.UserId);
            return new LogoutAllDevicesResult(false, 0, false, "Invalid password");
        }

        return null;
    }

    private async Task<(IReadOnlyList<Session> sessionsToRevoke, bool currentSessionExcluded)> GetSessionsToRevokeAsync(
        LogoutAllDevicesCommand request,
        CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            return (sessions, false);
        }

        if (request.IncludeCurrentSession || string.IsNullOrEmpty(request.CurrentSessionTokenHash))
        {
            return (sessions, false);
        }

        var filteredSessions = sessions
            .Where(s => !string.Equals(s.TokenHash, request.CurrentSessionTokenHash, StringComparison.Ordinal))
            .ToList()
            .AsReadOnly();

        var currentSessionExcluded = sessions.Count != filteredSessions.Count;
        return (filteredSessions, currentSessionExcluded);
    }

    private async Task<List<string>> RevokeSessionsAsync(
        IReadOnlyList<Session> sessions,
        CancellationToken cancellationToken)
    {
        var tokenHashesToInvalidate = new List<string>(sessions.Count);

        foreach (var session in sessions)
        {
            try
            {
                session.Revoke(_timeProvider, "Logout all devices requested by user");
                await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                tokenHashesToInvalidate.Add(session.TokenHash);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Failed to revoke session {SessionId}, may already be revoked", session.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tokenHashesToInvalidate;
    }

    private async Task InvalidateCacheAsync(List<string> tokenHashesToInvalidate, CancellationToken cancellationToken)
    {
        if (_sessionCache == null)
        {
            return;
        }

        foreach (var tokenHash in tokenHashesToInvalidate)
        {
            try
            {
                await _sessionCache.InvalidateAsync(tokenHash, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Service boundary - cache failure resilience for batch session operations
            // RESILIENCE: Cache failures should not prevent batch session revocation
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for session, continuing batch revocation");
            }
#pragma warning restore CA1031
        }
    }

    private static bool DetermineIfCurrentSessionRevoked(LogoutAllDevicesCommand request, List<string> tokenHashesToInvalidate)
    {
        if (!request.IncludeCurrentSession || string.IsNullOrEmpty(request.CurrentSessionTokenHash))
        {
            return false;
        }

        return tokenHashesToInvalidate.Contains(request.CurrentSessionTokenHash, StringComparer.Ordinal);
    }

    private void LogResult(Guid userId, int revokedCount, bool currentSessionExcluded, bool currentSessionRevoked)
    {
        var status = currentSessionExcluded ? "preserved" : (currentSessionRevoked ? "revoked" : "not specified");
        _logger.LogInformation(
            "User {UserId} logged out of {RevokedCount} devices. Current session: {CurrentSessionStatus}",
            userId, revokedCount, status);
    }
}
