using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for RecalculateBadgesCommand.
/// Recalculates badges for one or all users by evaluating eligibility and awarding/revoking as needed.
/// </summary>
internal sealed class RecalculateBadgesCommandHandler : IRequestHandler<RecalculateBadgesCommand, RecalculateBadgesResponse>
{
    private readonly IBadgeEvaluator _badgeEvaluator;
    private readonly IUserBadgeRepository _userBadgeRepository;
    private readonly IBadgeRepository _badgeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecalculateBadgesCommandHandler> _logger;

    public RecalculateBadgesCommandHandler(
        IBadgeEvaluator badgeEvaluator,
        IUserBadgeRepository userBadgeRepository,
        IBadgeRepository badgeRepository,
        IUnitOfWork unitOfWork,
        ILogger<RecalculateBadgesCommandHandler> logger)
    {
        _badgeEvaluator = badgeEvaluator;
        _userBadgeRepository = userBadgeRepository;
        _badgeRepository = badgeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RecalculateBadgesResponse> Handle(
        RecalculateBadgesCommand request,
        CancellationToken cancellationToken)
    {
        var usersProcessed = 0;
        var badgesAwarded = 0;
        var badgesRevoked = 0;

        try
        {
            // Get list of users to process
            var userIds = request.UserId.HasValue
                ? new List<Guid> { request.UserId.Value }
                : await GetAllDistinctUserIdsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Starting badge recalculation for {UserCount} user(s)",
                userIds.Count);

            // Get all active badges once (cached for all users)
            var allActiveBadges = await _badgeRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
            var badgeDict = allActiveBadges.ToDictionary(b => b.Id);

            foreach (var userId in userIds)
            {
                var (awarded, revoked) = await RecalculateBadgesForUserAsync(
                    userId,
                    badgeDict,
                    cancellationToken).ConfigureAwait(false);

                badgesAwarded += awarded;
                badgesRevoked += revoked;
                usersProcessed++;

                if (usersProcessed % 100 == 0)
                {
                    _logger.LogInformation(
                        "Processed {UsersProcessed} users: {BadgesAwarded} awarded, {BadgesRevoked} revoked",
                        usersProcessed,
                        badgesAwarded,
                        badgesRevoked);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Badge recalculation complete. {UsersProcessed} users, {BadgesAwarded} awarded, {BadgesRevoked} revoked",
                usersProcessed,
                badgesAwarded,
                badgesRevoked);

            return new RecalculateBadgesResponse
            {
                UsersProcessed = usersProcessed,
                BadgesAwarded = badgesAwarded,
                BadgesRevoked = badgesRevoked
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate badges");
            throw;
        }
    }

    private async Task<(int Awarded, int Revoked)> RecalculateBadgesForUserAsync(
        Guid userId,
        Dictionary<Guid, Badge> allActiveBadges,
        CancellationToken cancellationToken)
    {
        var badgesAwarded = 0;
        var badgesRevoked = 0;

        try
        {
            // Evaluate eligible badges
            var eligibleBadges = await _badgeEvaluator.EvaluateEligibleBadgesAsync(
                userId,
                cancellationToken).ConfigureAwait(false);

            var eligibleBadgeIds = eligibleBadges.Select(b => b.Id).ToHashSet();

            // Get current user badges
            var currentBadges = await _userBadgeRepository.GetByUserIdAsync(
                userId,
                includeHidden: true,
                cancellationToken).ConfigureAwait(false);

            // Award missing badges. currentBadges holds only the active ones: a badge the user lost is given
            // back by reinstating its row, not by a second one (#5116).
            foreach (var badge in eligibleBadges)
            {
                if (!currentBadges.Any(ub => ub.BadgeId == badge.Id && ub.IsActive)
                    && await UserBadgeGrant.GrantAsync(_userBadgeRepository, userId, badge, triggeringShareRequestId: null, cancellationToken).ConfigureAwait(false))
                {
                    badgesAwarded++;

                    _logger.LogDebug(
                        "Awarded badge {BadgeCode} to user {UserId}",
                        badge.Code,
                        userId);
                }
            }

            // Revoke invalid badges (user no longer qualifies)
            foreach (var userBadge in currentBadges.Where(ub => ub.IsActive))
            {
                // Only revoke if badge is still active AND user no longer qualifies
                if (allActiveBadges.TryGetValue(userBadge.BadgeId, out var badge) && !eligibleBadgeIds.Contains(userBadge.BadgeId))
                {
                    // Only a leaderboard badge can be lost; every other one is permanent once earned.
                    if (!IsRevocableBadge(badge))
                    {
                        _logger.LogDebug(
                            "Skipping revocation of permanent badge {BadgeCode} for user {UserId}",
                            badge.Code,
                            userId);
                        continue;
                    }

                    userBadge.Revoke("No longer meets badge requirements");
                    // #5116: GetByUserIdAsync reads AsNoTracking; without this the revocation stayed in memory.
                    await _userBadgeRepository.UpdateAsync(userBadge, cancellationToken).ConfigureAwait(false);
                    badgesRevoked++;

                    _logger.LogDebug(
                        "Revoked badge {BadgeId} from user {UserId}",
                        userBadge.BadgeId,
                        userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to recalculate badges for user {UserId}. Skipping user.",
                userId);
        }

        return (badgesAwarded, badgesRevoked);
    }

    /// <summary>
    /// #5116, decision of 2026-11-23: only a leaderboard badge (TopContributor: TOP_CONTRIBUTOR, TOP_THREE,
    /// NUMBER_ONE) is revocable, because «ranked in the top N» is a state. Every other badge is permanent:
    /// the milestones, the quality streaks («5 contributions approved without changes requested» tells a
    /// conquest, not a state), and Custom, which the evaluator never finds eligible, so with a blacklist it
    /// would be revoked at every recalculation. A new requirement type is permanent until it is added here.
    /// </summary>
    private static bool IsRevocableBadge(Badge badge) =>
        badge.Requirement.Type is BadgeRequirementType.TopContributor;

    private async Task<List<Guid>> GetAllDistinctUserIdsAsync(CancellationToken cancellationToken)
    {
        return await _userBadgeRepository.GetAllDistinctUserIdsAsync(cancellationToken).ConfigureAwait(false);
    }
}
