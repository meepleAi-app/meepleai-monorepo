using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Implementation of badge evaluation service.
/// </summary>
internal sealed class BadgeEvaluator : IBadgeEvaluator
{
    private readonly IBadgeRepository _badgeRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameDocumentRepository _sharedGameDocumentRepository;
    private readonly IMediator _mediator;
    private IReadOnlyDictionary<Guid, int>? _previousMonthRanks;

    public BadgeEvaluator(
        IBadgeRepository badgeRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameDocumentRepository sharedGameDocumentRepository,
        IMediator mediator)
    {
        _mediator = mediator;
        _badgeRepository = badgeRepository;
        _shareRequestRepository = shareRequestRepository;
        _sharedGameDocumentRepository = sharedGameDocumentRepository;
    }

    public async Task<List<Badge>> EvaluateEligibleBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allActiveBadges = await _badgeRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        var eligibleBadges = new List<Badge>();

        foreach (var badge in allActiveBadges)
        {
            if (await CheckBadgeRequirementAsync(userId, badge.Requirement, cancellationToken).ConfigureAwait(false))
            {
                eligibleBadges.Add(badge);
            }
        }

        return eligibleBadges;
    }

    public async Task<bool> CheckBadgeRequirementAsync(
        Guid userId,
        BadgeRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        return requirement.Type switch
        {
            BadgeRequirementType.FirstContribution =>
                await CheckFirstContributionAsync(userId, cancellationToken).ConfigureAwait(false),

            BadgeRequirementType.ContributionCount =>
                await CheckContributionCountAsync(userId, requirement.MinContributions!.Value, cancellationToken).ConfigureAwait(false),

            BadgeRequirementType.DocumentCount =>
                await CheckDocumentCountAsync(userId, requirement.MinDocuments!.Value, cancellationToken).ConfigureAwait(false),

            BadgeRequirementType.QualityStreak =>
                await CheckQualityStreakAsync(userId, requirement.ConsecutiveApprovalsWithoutChanges!.Value, cancellationToken).ConfigureAwait(false),

            // #5136: it answered false ("handled by scheduled job") while the job asked this evaluator, so no leaderboard
            // badge was ever awarded. Decision of 2026-11-23: the previous calendar month, judged once it is over.
            BadgeRequirementType.TopContributor =>
                await IsRankedInPreviousMonthAsync(userId, requirement.TopContributorRank!.Value, cancellationToken).ConfigureAwait(false),

            _ => false
        };
    }

    /// <summary>
    /// Whether <paramref name="userId"/> ranks within <paramref name="topRank"/> among the contributors of the previous
    /// calendar month (#5136): 10 for TOP_CONTRIBUTOR, 3 for TOP_THREE, 1 for NUMBER_ONE. Ties share a rank. The ranks of a
    /// closed month do not change, so they are read once per evaluator, not once per badge.
    /// </summary>
    private async Task<bool> IsRankedInPreviousMonthAsync(Guid userId, int topRank, CancellationToken cancellationToken)
    {
        _previousMonthRanks ??= await _mediator
            .Send(new GetPreviousMonthContributionRanksQuery(), cancellationToken)
            .ConfigureAwait(false);
        return _previousMonthRanks.TryGetValue(userId, out var rank) && rank <= topRank;
    }

    private async Task<bool> CheckFirstContributionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var count = await _shareRequestRepository.CountApprovedByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return count == 1;
    }

    private async Task<bool> CheckContributionCountAsync(Guid userId, int minCount, CancellationToken cancellationToken)
    {
        var count = await _shareRequestRepository.CountApprovedByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return count >= minCount;
    }

    private async Task<bool> CheckDocumentCountAsync(Guid userId, int minDocuments, CancellationToken cancellationToken)
    {
        var documentCount = await _sharedGameDocumentRepository.CountByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return documentCount >= minDocuments;
    }

    private async Task<bool> CheckQualityStreakAsync(Guid userId, int minStreak, CancellationToken cancellationToken)
    {
        var recentRequests = await _shareRequestRepository.GetRecentResolvedByUserAsync(userId, minStreak, cancellationToken).ConfigureAwait(false);

        if (recentRequests.Count < minStreak)
            return false;

        // All must be approved (History tracking requires separate implementation)
        return recentRequests.All(r => r.Status == ShareRequestStatus.Approved);
    }
}
