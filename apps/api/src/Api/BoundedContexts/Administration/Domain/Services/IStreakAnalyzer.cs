using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Analyzes user's activity streak to generate maintenance nudges.
/// </summary>
public interface IStreakAnalyzer
{
    /// <summary>
    /// Generates streak maintenance insights based on user's current streak status.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of streak nudge insights (0-1 nudge)</returns>
    /// <remarks>
    /// Nudges user to maintain active streak or recover broken streak.
    /// Only generates insight if streak is at risk or recently broken.
    /// </remarks>
    Task<List<AIInsight>> AnalyzeStreakAsync(Guid userId, CancellationToken cancellationToken = default);
}
