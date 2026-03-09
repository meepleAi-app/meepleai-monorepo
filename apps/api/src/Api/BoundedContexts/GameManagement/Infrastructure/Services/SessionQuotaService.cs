using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SessionQuotaService> _logger;

    public SessionQuotaService(
        IGameSessionRepository sessionRepository,
        IConfigurationService configService,
        IUnitOfWork unitOfWork,
        ILogger<SessionQuotaService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionRepository);
        _sessionRepository = sessionRepository;
        ArgumentNullException.ThrowIfNull(configService);
        _configService = configService;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
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

    /// <summary>
    /// Ensures user can create a new session by checking quota and auto-terminating oldest sessions if needed.
    /// Issue #3671: Session limits enforcement with automatic termination.
    /// </summary>
    public async Task<SessionQuotaEnsureResult> EnsureQuotaAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        ArgumentNullException.ThrowIfNull(userRole);

        // Check quota first
        var quotaResult = await CheckQuotaAsync(userId, userTier, userRole, cancellationToken).ConfigureAwait(false);

        // If allowed (quota available or unlimited), return success immediately
        if (quotaResult.IsAllowed)
        {
            return SessionQuotaEnsureResult.Success();
        }

        // Quota exceeded - calculate how many sessions to terminate
        var sessionsToTerminate = quotaResult.CurrentCount - quotaResult.MaxAllowed + 1;

        _logger.LogInformation(
            "User {UserId} quota exceeded ({CurrentCount}/{MaxAllowed}). Terminating {Count} oldest session(s)",
            userId, quotaResult.CurrentCount, quotaResult.MaxAllowed, sessionsToTerminate);

        // Find oldest active sessions for this user
        var oldestSessions = await _sessionRepository.FindOldestActiveByUserIdAsync(
            userId,
            sessionsToTerminate,
            cancellationToken).ConfigureAwait(false);

        // Terminate each session (raises domain events)
        var terminatedIds = new List<Guid>();
        foreach (var session in oldestSessions)
        {
            session.TerminateForQuota("QuotaExceeded");
            terminatedIds.Add(session.Id);
        }

        // Persist changes (domain events will be dispatched by SaveChanges interceptor)
        foreach (var session in oldestSessions)
        {
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        }

        // CRITICAL: Save changes to persist terminated sessions and dispatch domain events
        // Pattern: All command handlers call SaveChangesAsync after UpdateAsync (e.g., CompleteGameSessionCommandHandler)
        // Without this, terminated sessions remain active and notifications are never sent
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Terminated {Count} session(s) for user {UserId}: {SessionIds}",
            terminatedIds.Count, userId, string.Join(", ", terminatedIds));

        return SessionQuotaEnsureResult.SuccessWithTerminations(
            terminatedIds,
            $"Terminated {terminatedIds.Count} oldest session(s) to enforce {userTier.Value} tier limit of {quotaResult.MaxAllowed}");
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
