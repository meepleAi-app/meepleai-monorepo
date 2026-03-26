using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetUserRateLimitStatus;

/// <summary>
/// Handler for GetUserRateLimitStatusQuery.
/// Retrieves complete rate limit status for a user including usage, limits, and override information.
/// </summary>
internal sealed class GetUserRateLimitStatusQueryHandler
    : IRequestHandler<GetUserRateLimitStatusQuery, UserRateLimitStatusDto>
{
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly IUserProfileReadService _userProfileReadService;
    private readonly IUserRateLimitOverrideRepository _overrideRepository;

    public GetUserRateLimitStatusQueryHandler(
        IRateLimitEvaluator rateLimitEvaluator,
        IUserProfileReadService userProfileReadService,
        IUserRateLimitOverrideRepository overrideRepository)
    {
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
        _userProfileReadService = userProfileReadService ?? throw new ArgumentNullException(nameof(userProfileReadService));
        _overrideRepository = overrideRepository ?? throw new ArgumentNullException(nameof(overrideRepository));
    }

    public async Task<UserRateLimitStatusDto> Handle(
        GetUserRateLimitStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get user profile for display name
        var userProfile = await _userProfileReadService.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", query.UserId.ToString());

        // Get rate limit status from evaluator
        var status = await _rateLimitEvaluator.GetUserStatusAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        // Get override details if present
        UserRateLimitOverrideDto? overrideDto = null;
        if (status.HasOverride)
        {
            var userOverride = await _overrideRepository.GetByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
            if (userOverride != null)
            {
                // Get admin name for override
                var admin = await _userProfileReadService.GetByIdAsync(userOverride.CreatedByAdminId, cancellationToken).ConfigureAwait(false);

                overrideDto = new UserRateLimitOverrideDto
                {
                    Id = userOverride.Id,
                    UserId = userOverride.UserId,
                    MaxPendingRequests = userOverride.MaxPendingRequests,
                    MaxRequestsPerMonth = userOverride.MaxRequestsPerMonth,
                    CooldownAfterRejection = userOverride.CooldownAfterRejection,
                    ExpiresAt = userOverride.ExpiresAt,
                    Reason = userOverride.Reason,
                    CreatedAt = userOverride.CreatedAt,
                    CreatedByAdminName = admin?.DisplayName ?? "Unknown",
                    IsActive = !userOverride.IsExpired()
                };
            }
        }

        // Map to DTO
        return new UserRateLimitStatusDto
        {
            UserId = query.UserId,
            UserName = userProfile.DisplayName ?? "Unknown",
            Tier = status.Tier,
            CurrentPendingCount = status.CurrentPendingCount,
            CurrentMonthlyCount = status.CurrentMonthlyCount,
            LastRejectionAt = status.LastRejectionAt,
            EffectiveMaxPending = status.EffectiveMaxPending,
            EffectiveMaxPerMonth = status.EffectiveMaxPerMonth,
            EffectiveCooldown = status.EffectiveCooldown,
            HasOverride = status.HasOverride,
            Override = overrideDto,
            CanCreateRequest = status.CanCreateRequest,
            BlockReason = status.BlockReason,
            CooldownEndsAt = status.CooldownEndsAt,
            MonthResetAt = status.MonthResetAt,
            PendingUsagePercent = status.PendingUsagePercent,
            MonthlyUsagePercent = status.MonthlyUsagePercent
        };
    }
}
