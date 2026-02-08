using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Services; // IHybridCacheService
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Cache DTO for token limits (reference type required by HybridCache)
/// </summary>
internal sealed record TokenLimitsCacheDto(bool Exceeded, int Remaining, bool IsBlocked);

/// <summary>
/// Implementation of token tracking service with limit enforcement (Issue #3786)
/// </summary>
internal sealed class TokenTrackingService : ITokenTrackingService
{
    private readonly IUserTokenUsageRepository _usageRepository;
    private readonly ITokenTierRepository _tierRepository;
    private readonly ILogger<TokenTrackingService> _logger;
    private readonly IHybridCacheService _cache;

    public TokenTrackingService(
        IUserTokenUsageRepository usageRepository,
        ITokenTierRepository tierRepository,
        ILogger<TokenTrackingService> logger,
        IHybridCacheService cache)
    {
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _tierRepository = tierRepository ?? throw new ArgumentNullException(nameof(tierRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<(bool exceeded, int remaining)> TrackUsageAsync(
        Guid userId,
        int tokensConsumed,
        decimal cost,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (tokensConsumed <= 0)
            throw new ArgumentException("Tokens consumed must be positive", nameof(tokensConsumed));

        // Get or create usage record
        var usage = await _usageRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (usage == null)
        {
            // Create new usage record for user (default: Free tier)
            var freeTier = await _tierRepository.GetByNameAsync(TierName.Free, cancellationToken).ConfigureAwait(false);
            if (freeTier == null)
                throw new InvalidOperationException("Free tier not found in database");

            usage = UserTokenUsage.Create(userId, freeTier.Id);
            await _usageRepository.AddAsync(usage, cancellationToken).ConfigureAwait(false);
        }

        // Get tier limits
        var tier = await _tierRepository.GetByIdAsync(usage.TierId, cancellationToken).ConfigureAwait(false);
        if (tier == null)
            throw new InvalidOperationException($"Tier {usage.TierId} not found");

        // Check if already blocked
        if (usage.IsBlocked)
        {
            _logger.LogWarning("User {UserId} attempted to use tokens while blocked", userId);
            return (true, 0);
        }

        // Record usage
        usage.RecordUsage(tokensConsumed, cost);

        // Check limits and update status
        usage.CheckLimits(tier.Limits);

        // Update in database
        await _usageRepository.UpdateAsync(usage, cancellationToken).ConfigureAwait(false);

        // Soft warning at 80% (log + event)
        if (usage.IsNearLimit && !usage.IsBlocked)
        {
            _logger.LogWarning(
                "User {UserId} approaching token limit: {Used}/{Limit} ({Percentage}%)",
                userId,
                usage.TokensUsed,
                tier.Limits.TokensPerMonth,
                (double)usage.TokensUsed / tier.Limits.TokensPerMonth * 100);

            // Warnings are tracked automatically in CheckLimits() call above
        }

        // Hard block at 100% (exception + event)
        if (usage.IsBlocked)
        {
            _logger.LogError(
                "User {UserId} exceeded token limit: {Used}/{Limit} - BLOCKED",
                userId,
                usage.TokensUsed,
                tier.Limits.TokensPerMonth);

            // Return exceeded with 0 remaining
            return (true, 0);
        }

        // Calculate remaining tokens
        int remaining = tier.Limits.TokensPerMonth - usage.TokensUsed;

        // Invalidate cache
        await _cache.RemoveAsync($"token_usage:{userId}", cancellationToken).ConfigureAwait(false);

        return (false, remaining);
    }

    public async Task<(bool exceeded, int remaining, bool isBlocked)> CheckLimitsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        // Try cache first
        var cacheKey = $"token_limits:{userId}";
        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var usage = await _usageRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
                if (usage == null) return new TokenLimitsCacheDto(false, int.MaxValue, false); // No usage record = unlimited (for now)

                var tier = await _tierRepository.GetByIdAsync(usage.TierId, ct).ConfigureAwait(false);
                if (tier == null) return new TokenLimitsCacheDto(false, int.MaxValue, false);

                int limit = tier.Limits.TokensPerMonth;
                bool exceeded = usage.TokensUsed >= limit;
                int remaining = exceeded ? 0 : limit - usage.TokensUsed;

                return new TokenLimitsCacheDto(exceeded, remaining, usage.IsBlocked);
            },
            tags: null,
            expiration: TimeSpan.FromMinutes(5),
            ct: cancellationToken).ConfigureAwait(false);

        return (cached.Exceeded, cached.Remaining, cached.IsBlocked);
    }

    public async Task<(int tokensUsed, int messagesCount, decimal cost, int limit, double percentageUsed)> GetUserUsageAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        var usage = await _usageRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (usage == null)
            return (0, 0, 0m, int.MaxValue, 0.0); // No usage record

        var tier = await _tierRepository.GetByIdAsync(usage.TierId, cancellationToken).ConfigureAwait(false);
        if (tier == null)
            throw new InvalidOperationException($"Tier {usage.TierId} not found");

        int limit = tier.Limits.TokensPerMonth;
        double percentageUsed = (double)usage.TokensUsed / limit * 100.0;

        return (usage.TokensUsed, usage.MessagesCount, usage.Cost, limit, percentageUsed);
    }

    public async Task ResetMonthlyUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        var usage = await _usageRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (usage == null)
        {
            _logger.LogWarning("Attempted to reset usage for non-existent user {UserId}", userId);
            return;
        }

        usage.ResetMonthlyUsage();
        await _usageRepository.UpdateAsync(usage, cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        await _cache.RemoveAsync($"token_usage:{userId}", cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync($"token_limits:{userId}", cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Monthly usage reset for user {UserId}", userId);
    }

    public async Task ResetAllUsersMonthlyUsageAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting monthly reset for all users");

        var allUsages = await _usageRepository.GetTopConsumersAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);

        int resetCount = 0;
        foreach (var usage in allUsages)
        {
            usage.ResetMonthlyUsage();
            await _usageRepository.UpdateAsync(usage, cancellationToken).ConfigureAwait(false);

            // Invalidate caches
            await _cache.RemoveAsync($"token_usage:{usage.UserId}", cancellationToken).ConfigureAwait(false);
            await _cache.RemoveAsync($"token_limits:{usage.UserId}", cancellationToken).ConfigureAwait(false);

            resetCount++;
        }

        _logger.LogInformation("Monthly reset completed: {Count} users reset", resetCount);
    }

    public async Task<List<(Guid userId, int tokensUsed, int limit, double percentageUsed)>> GetUsersNearingLimitAsync(
        int thresholdPercentage = 80,
        CancellationToken cancellationToken = default)
    {
        if (thresholdPercentage < 0 || thresholdPercentage > 100)
            throw new ArgumentException("Threshold must be between 0 and 100", nameof(thresholdPercentage));

        var allUsages = await _usageRepository.GetTopConsumersAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);
        var results = new List<(Guid userId, int tokensUsed, int limit, double percentageUsed)>();

        foreach (var usage in allUsages)
        {
            var tier = await _tierRepository.GetByIdAsync(usage.TierId, cancellationToken).ConfigureAwait(false);
            if (tier == null) continue;

            int limit = tier.Limits.TokensPerMonth;
            double percentageUsed = (double)usage.TokensUsed / limit * 100.0;

            if (percentageUsed >= thresholdPercentage)
            {
                results.Add((usage.UserId, usage.TokensUsed, limit, percentageUsed));
            }
        }

        return results.OrderByDescending(r => r.percentageUsed).ToList();
    }
}
