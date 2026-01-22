using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
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

            // Award missing badges
            foreach (var badge in eligibleBadges)
            {
                if (!currentBadges.Any(ub => ub.BadgeId == badge.Id && ub.IsActive))
                {
                    var userBadge = UserBadge.Award(userId, badge.Id, badge.Code, triggeringShareRequestId: null);
                    await _userBadgeRepository.AddAsync(userBadge, cancellationToken).ConfigureAwait(false);
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
                if (allActiveBadges.ContainsKey(userBadge.BadgeId) && !eligibleBadgeIds.Contains(userBadge.BadgeId))
                {
                    userBadge.Revoke("No longer meets badge requirements");
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

    private Task<List<Guid>> GetAllDistinctUserIdsAsync(CancellationToken cancellationToken)
    {
        // IMPLEMENTATION NOTE:
        // This is a stub implementation. In production, you would:
        // 1. Query IUserRepository.GetAllUserIdsAsync() (if available)
        // 2. Or query ShareRequestRepository for distinct user IDs
        // 3. Or use a dedicated IContributorRepository
        //
        // For now, return empty list to avoid runtime errors
        // This means recalculate-all-users will process 0 users (safe fallback)

        _logger.LogWarning(
            "GetAllDistinctUserIdsAsync not fully implemented. " +
            "Use RecalculateBadgesCommand with specific UserId for targeted recalculation.");

        return Task.FromResult(new List<Guid>());
    }
}
