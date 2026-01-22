using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;

/// <summary>
/// Implementation of IRateLimitEvaluator for share request rate limiting.
/// Uses tier-based limits with user-specific override support and efficient database queries.
/// Issue #2730: Rate Limit Queries and Commands.
/// </summary>
internal sealed class RateLimitEvaluator : IRateLimitEvaluator
{
    private readonly IRateLimitConfigRepository _configRepository;
    private readonly IUserRateLimitOverrideRepository _overrideRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<RateLimitEvaluator> _logger;

    private static readonly TimeSpan TierConfigCacheDuration = TimeSpan.FromMinutes(15);

    public RateLimitEvaluator(
        IRateLimitConfigRepository configRepository,
        IUserRateLimitOverrideRepository overrideRepository,
        IShareRequestRepository shareRequestRepository,
        IUserRepository userRepository,
        IHybridCacheService cache,
        ILogger<RateLimitEvaluator> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _overrideRepository = overrideRepository ?? throw new ArgumentNullException(nameof(overrideRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RateLimitStatus> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Get user to determine tier
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", userId.ToString());

        // Map Authentication.UserTier to SystemConfiguration.UserTier
        var tier = MapUserTier(user);

        // Admin tier has unlimited access
        if (tier == UserTier.Admin)
        {
            return RateLimitStatus.CreateUnlimited(userId);
        }

        // Get tier config (cached)
        var tierConfig = await GetTierConfigCachedAsync(tier, cancellationToken).ConfigureAwait(false);

        // Get user override if any (not cached - real-time accuracy needed)
        var userOverride = await _overrideRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // Calculate effective limits (override wins if present)
        var effectiveMaxPending = userOverride?.MaxPendingRequests ?? tierConfig.MaxPendingRequests;
        var effectiveMaxPerMonth = userOverride?.MaxRequestsPerMonth ?? tierConfig.MaxRequestsPerMonth;
        var effectiveCooldown = userOverride?.CooldownAfterRejection ?? tierConfig.CooldownAfterRejection;

        // Get current usage (efficient queries)
        var pendingCount = await _shareRequestRepository.CountPendingByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var monthlyCount = await _shareRequestRepository.CountThisMonthByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var lastRejectionDate = await _shareRequestRepository.GetLastRejectionDateAsync(userId, cancellationToken).ConfigureAwait(false);

        // Create status using domain factory method
        return RateLimitStatus.Create(
            userId,
            tier,
            hasOverride: userOverride != null,
            pendingCount,
            monthlyCount,
            lastRejectionDate,
            effectiveMaxPending,
            effectiveMaxPerMonth,
            effectiveCooldown);
    }

    public async Task<bool> CanUserCreateRequestAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var status = await GetUserStatusAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!status.CanCreateRequest)
        {
            _logger.LogInformation(
                "User {UserId} (Tier: {Tier}) blocked from creating share request: {Reason}. " +
                "Pending: {Pending}/{MaxPending}, Monthly: {Monthly}/{MaxMonthly}",
                userId,
                status.Tier,
                status.BlockReason,
                status.CurrentPendingCount,
                status.EffectiveMaxPending,
                status.CurrentMonthlyCount,
                status.EffectiveMaxPerMonth);
        }

        return status.CanCreateRequest;
    }

    public async Task RecordRejectionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // The rejection is recorded in the ShareRequest entity itself (ResolvedAt + Status=Rejected).
        // This method provides a hook for additional tracking/notifications if needed.
        _logger.LogInformation(
            "Share request rejection recorded for user {UserId}. Cooldown period started.",
            userId);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task<Domain.Entities.ShareRequestLimitConfig> GetTierConfigCachedAsync(
        UserTier tier,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"rate_limit_config_{tier}";

        return await _cache.GetOrCreateAsync<Domain.Entities.ShareRequestLimitConfig>(
            cacheKey,
            async (ct) =>
            {
                var config = await _configRepository.GetByTierAsync(tier, ct).ConfigureAwait(false);
                return config ?? throw new NotFoundException("ShareRequestLimitConfig", tier.ToString());
            },
            null,
            TierConfigCacheDuration,
            cancellationToken).ConfigureAwait(false);
    }

    private static UserTier MapUserTier(Authentication.Domain.Entities.User user)
    {
        // Map Authentication.UserTier (value object) to SystemConfiguration.UserTier (enum)
        // Authentication.UserTier: "free", "normal", "premium"
        // SystemConfiguration.UserTier: Free(0), Premium(1), Pro(2), Admin(3)

        // Check if user is admin via Role (Admin is Role-based, not Tier-based)
        if (user.Role.Value.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return UserTier.Admin;
        }

        return user.Tier.Value switch
        {
            "free" => UserTier.Free,
            "normal" => UserTier.Premium,
            "premium" => UserTier.Pro,
            _ => UserTier.Free
        };
    }
}

