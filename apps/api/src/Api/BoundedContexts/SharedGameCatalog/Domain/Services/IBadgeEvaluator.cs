using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Domain service for evaluating badge eligibility based on user contributions and requirements.
/// </summary>
public interface IBadgeEvaluator
{
    /// <summary>
    /// Evaluates all active badges for a user and returns those they are eligible for.
    /// </summary>
    /// <param name="userId">The ID of the user to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of badges the user is currently eligible for.</returns>
    Task<List<Badge>> EvaluateEligibleBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user meets the requirements for a specific badge.
    /// </summary>
    /// <param name="userId">The ID of the user to check.</param>
    /// <param name="requirement">The badge requirement to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user meets the requirement, otherwise false.</returns>
    Task<bool> CheckBadgeRequirementAsync(
        Guid userId,
        BadgeRequirement requirement,
        CancellationToken cancellationToken = default);
}
