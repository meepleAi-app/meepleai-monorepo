using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Event handler that evaluates and awards badges when a share request is approved.
/// </summary>
internal sealed class BadgeEvaluationOnApprovalHandler : DomainEventHandlerBase<ShareRequestApprovedEvent>
{
    private readonly IBadgeEvaluator _badgeEvaluator;
    private readonly IUserBadgeRepository _userBadgeRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BadgeEvaluationOnApprovalHandler(
        MeepleAiDbContext dbContext,
        IBadgeEvaluator badgeEvaluator,
        IUserBadgeRepository userBadgeRepository,
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<BadgeEvaluationOnApprovalHandler> logger)
        : base(dbContext, logger)
    {
        _badgeEvaluator = badgeEvaluator;
        _userBadgeRepository = userBadgeRepository;
        _shareRequestRepository = shareRequestRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleEventAsync(
        ShareRequestApprovedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get share request to extract user ID
            var shareRequest = await _shareRequestRepository.GetByIdAsync(
                domainEvent.ShareRequestId,
                cancellationToken).ConfigureAwait(false);

            if (shareRequest == null)
            {
                Logger.LogWarning(
                    "Share request {ShareRequestId} not found during badge evaluation",
                    domainEvent.ShareRequestId);
                return;
            }

            // Evaluate all eligible badges for this user
            var eligibleBadges = await _badgeEvaluator.EvaluateEligibleBadgesAsync(
                shareRequest.UserId,
                cancellationToken).ConfigureAwait(false);

            if (eligibleBadges.Count == 0)
            {
                Logger.LogInformation(
                    "No new badges eligible for user {UserId} after share request approval",
                    shareRequest.UserId);
                return;
            }

            // Get badges user already has
            var existingBadgeIds = await _userBadgeRepository.GetBadgeIdsByUserAsync(
                shareRequest.UserId,
                cancellationToken).ConfigureAwait(false);

            // Award new badges
            var badgesAwarded = 0;
            foreach (var badge in eligibleBadges)
            {
                if (!existingBadgeIds.Contains(badge.Id))
                {
                    var userBadge = UserBadge.Award(
                        shareRequest.UserId,
                        badge.Id,
                        badge.Code,
                        domainEvent.ShareRequestId);

                    await _userBadgeRepository.AddAsync(userBadge, cancellationToken).ConfigureAwait(false);
                    badgesAwarded++;

                    Logger.LogInformation(
                        "Badge {BadgeCode} awarded to user {UserId} via share request {ShareRequestId}",
                        badge.Code,
                        shareRequest.UserId,
                        domainEvent.ShareRequestId);
                }
            }

            if (badgesAwarded > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogInformation(
                    "{BadgeCount} new badges awarded to user {UserId}",
                    badgesAwarded,
                    shareRequest.UserId);
            }
        }
        catch (Exception ex)
        {
            // Badge evaluation failure should NOT fail the approval flow
            Logger.LogError(
                ex,
                "Failed to evaluate badges for share request {ShareRequestId}. Badge assignment skipped.",
                domainEvent.ShareRequestId);
        }
    }

    protected override Guid? GetUserId(ShareRequestApprovedEvent domainEvent)
        => domainEvent.AdminId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestApprovedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["TargetSharedGameId"] = domainEvent.TargetSharedGameId
        };
}
