using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Services;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Database-based implementation of session quota service.
/// Tracks active concurrent sessions with tier-based limits.
/// </summary>
internal sealed class SessionQuotaService : ISessionQuotaService
{
    /// <summary>
    /// Default session limits for each tier.
    /// </summary>
    private static class DefaultLimits
    {
        public const int FreeMaxSessions = 3;
        public const int NormalMaxSessions = 10;
        public const int PremiumMaxSessions = -1; // Unlimited
    }

    private readonly ISessionRepository _sessionRepository;
    private readonly IConfigurationService _configService;
    private readonly ILogger<SessionQuotaService> _logger;

    public SessionQuotaService(
        ISessionRepository sessionRepository,
        IConfigurationService configService,
        ILogger<SessionQuotaService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionRepository);
        _sessionRepository = sessionRepository;
        ArgumentNullException.ThrowIfNull(configService);
        _configService = configService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<SessionQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Admin and Editor bypass quota checks (unlimited)
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            return SessionQuotaResult.Unlimited();
        }

        var maxSessions = await GetLimitForTierAsync(userTier).ConfigureAwait(false);

        // Premium tier with unlimited sessions
        if (maxSessions < 0)
        {
            return SessionQuotaResult.Unlimited();
        }

        var currentCount = await CountActiveSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        // Check if user has reached the limit
        if (currentCount >= maxSessions)
        {
            _logger.LogInformation(
                "User {UserId} has reached session limit: {CurrentCount}/{MaxSessions} for {Tier} tier",
                userId, currentCount, maxSessions, userTier.Value);

            return SessionQuotaResult.Denied(
                $"Concurrent session limit reached ({maxSessions} sessions for {userTier.Value} tier). Close an active session or upgrade your subscription.",
                currentCount,
                maxSessions);
        }

        return SessionQuotaResult.Allowed(currentCount, maxSessions);
    }

    public async Task<SessionQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Admin and Editor have unlimited quota
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            // Still return actual count for admins (useful for analytics)
            var adminCount = await CountActiveSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return new SessionQuotaInfo
            {
                ActiveSessions = adminCount,
                MaxSessions = int.MaxValue,
                RemainingSlots = int.MaxValue,
                IsUnlimited = true
            };
        }

        var maxSessions = await GetLimitForTierAsync(userTier).ConfigureAwait(false);

        // Premium tier with unlimited sessions
        if (maxSessions < 0)
        {
            var premiumCount = await CountActiveSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return new SessionQuotaInfo
            {
                ActiveSessions = premiumCount,
                MaxSessions = int.MaxValue,
                RemainingSlots = int.MaxValue,
                IsUnlimited = true
            };
        }

        var currentCount = await CountActiveSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        var remaining = Math.Max(0, maxSessions - currentCount);

        return new SessionQuotaInfo
        {
            ActiveSessions = currentCount,
            MaxSessions = maxSessions,
            RemainingSlots = remaining,
            IsUnlimited = false
        };
    }

    private async Task<int> GetLimitForTierAsync(UserTier tier)
    {
        var configKey = $"SessionLimits:{tier.Value}:MaxSessions";

        // Get from configuration service (with fallback to defaults)
        var configuredLimit = await _configService.GetValueAsync<int?>(configKey, defaultValue: null).ConfigureAwait(false);

        if (configuredLimit.HasValue)
        {
            return configuredLimit.Value;
        }

        // Return default limits
        return tier.Value switch
        {
            "free" => DefaultLimits.FreeMaxSessions,
            "normal" => DefaultLimits.NormalMaxSessions,
            "premium" => DefaultLimits.PremiumMaxSessions, // -1 = unlimited
            _ => DefaultLimits.FreeMaxSessions // Default to Free tier limits for unknown tiers
        };
    }

    private async Task<int> CountActiveSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Count active sessions (repository method already filters EndDate IS NULL)
        var activeSessions = await _sessionRepository.GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return activeSessions.Count();
    }
}
