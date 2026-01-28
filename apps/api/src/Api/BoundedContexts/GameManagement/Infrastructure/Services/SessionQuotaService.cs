using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Services;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Database-based implementation of game session quota service.
/// Tracks active sessions with tier-based limits.
/// Issue #3070: Session limits backend implementation.
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
        public const int PremiumMaxSessions = -1; // -1 = unlimited
    }

    private readonly IGameSessionRepository _sessionRepository;
    private readonly IConfigurationService _configService;
    private readonly ILogger<SessionQuotaService> _logger;

    public SessionQuotaService(
        IGameSessionRepository sessionRepository,
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
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Admin and Editor bypass quota checks (unlimited)
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            return SessionQuotaResult.Unlimited();
        }

        // Premium tier has unlimited sessions
        if (userTier.IsPremium())
        {
            var premiumCount = await _sessionRepository.CountActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
            return SessionQuotaResult.Unlimited(premiumCount);
        }

        var maxSessions = await GetLimitForTierAsync(userTier).ConfigureAwait(false);
        var currentCount = await _sessionRepository.CountActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // Check if user has reached the limit
        if (currentCount >= maxSessions)
        {
            _logger.LogInformation(
                "User {UserId} has reached session limit: {CurrentCount}/{MaxSessions} for {Tier} tier",
                userId, currentCount, maxSessions, userTier.Value);

            return SessionQuotaResult.Denied(
                $"Session limit reached ({maxSessions} active sessions for {userTier.Value} tier). Complete or abandon existing sessions, or upgrade your subscription.",
                currentCount,
                maxSessions);
        }

        return SessionQuotaResult.Allowed(currentCount, maxSessions);
    }

    public async Task<SessionQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        var currentCount = await _sessionRepository.CountActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // Admin and Editor have unlimited quota
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            return SessionQuotaInfo.Unlimited(currentCount);
        }

        // Premium tier has unlimited sessions
        if (userTier.IsPremium())
        {
            return SessionQuotaInfo.Unlimited(currentCount);
        }

        var maxSessions = await GetLimitForTierAsync(userTier).ConfigureAwait(false);
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
            "premium" => DefaultLimits.PremiumMaxSessions,
            _ => DefaultLimits.FreeMaxSessions // Default to Free tier limits for unknown tiers
        };
    }
}
