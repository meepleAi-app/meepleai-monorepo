using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Implementation of badge evaluation service.
/// </summary>
internal sealed class BadgeEvaluator : IBadgeEvaluator
{
    private readonly IBadgeRepository _badgeRepository;
    private readonly IShareRequestRepository _shareRequestRepository;

    public BadgeEvaluator(
        IBadgeRepository badgeRepository,
        IShareRequestRepository shareRequestRepository)
    {
        _badgeRepository = badgeRepository;
        _shareRequestRepository = shareRequestRepository;
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

            BadgeRequirementType.TopContributor =>
                false, // Handled by scheduled job

            _ => false
        };
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

    private Task<bool> CheckDocumentCountAsync(Guid userId, int minDocuments, CancellationToken cancellationToken)
    {
        // Document counting requires integration with document tracking system (future enhancement)
        return Task.FromResult(false);
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
