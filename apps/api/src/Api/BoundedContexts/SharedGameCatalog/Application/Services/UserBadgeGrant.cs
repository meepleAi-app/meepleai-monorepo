using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #5116: the one way to give a badge to a user. A user who held it and lost it keeps the revoked row, and the
/// unique (UserId, BadgeId) index refuses a second one with 23505, so that row is reinstated; otherwise the badge
/// is awarded. <c>RecalculateBadges</c> and <c>BadgeEvaluationOnApprovalHandler</c> both go through here: an
/// <see cref="UserBadge.Award"/> next to a revoked row fails the whole SaveChanges, and the approval handler
/// swallows that failure together with every other badge of the same approval.
/// </summary>
internal static class UserBadgeGrant
{
    /// <returns><c>true</c> if the user did not hold the badge and now does.</returns>
    public static async Task<bool> GrantAsync(
        IUserBadgeRepository repository,
        Guid userId,
        Badge badge,
        Guid? triggeringShareRequestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(badge);

        var held = await repository.GetByUserAndBadgeAsync(userId, badge.Id, cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            await repository
                .AddAsync(UserBadge.Award(userId, badge.Id, badge.Code, triggeringShareRequestId), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (held.IsActive)
            return false;

        held.Reinstate();
        await repository.UpdateAsync(held, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
