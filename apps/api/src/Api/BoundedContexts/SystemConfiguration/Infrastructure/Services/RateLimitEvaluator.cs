using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Constants;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;

/// <summary>
/// Implementation of IRateLimitEvaluator for share request rate limiting.
/// Uses tier-based limits with user-specific override support.
/// Issue #2724: CreateShareRequest Command infrastructure.
/// </summary>
internal sealed class RateLimitEvaluator : IRateLimitEvaluator
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ILogger<RateLimitEvaluator> _logger;

    public RateLimitEvaluator(
        IShareRequestRepository shareRequestRepository,
        ILogger<RateLimitEvaluator> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RateLimitStatus> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Default to Free tier - user tier service integration pending
        var tier = UserTier.Free;

        // Get pending requests for this user
        var userRequests = await _shareRequestRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        var pendingCount = userRequests.Count(r =>
            r.Status == ShareRequestStatus.Pending ||
            r.Status == ShareRequestStatus.InReview ||
            r.Status == ShareRequestStatus.ChangesRequested);

        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyCount = userRequests.Count(r => r.CreatedAt >= currentMonth);

        // Get last rejection time
        var lastRejection = userRequests
            .Where(r => r.Status == ShareRequestStatus.Rejected)
            .OrderByDescending(r => r.ResolvedAt)
            .FirstOrDefault();

        // Get limits based on tier
        var limits = DefaultRateLimitConfigs.GetByTier(tier);

        return RateLimitStatus.Create(
            userId,
            tier,
            hasOverride: false,
            pendingCount,
            monthlyCount,
            lastRejection?.ResolvedAt,
            limits.MaxPendingRequests,
            limits.MaxRequestsPerMonth,
            limits.CooldownAfterRejection);
    }

    public async Task<bool> CanUserCreateRequestAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var status = await GetUserStatusAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!status.CanCreateRequest)
        {
            _logger.LogInformation(
                "User {UserId} blocked from creating share request: {Reason}",
                userId, status.BlockReason);
        }

        return status.CanCreateRequest;
    }

    public async Task RecordRejectionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // The rejection is recorded in the ShareRequest entity itself.
        // This method provides a hook for additional tracking if needed.
        _logger.LogInformation(
            "Share request rejection recorded for user {UserId}. Cooldown period started.",
            userId);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
