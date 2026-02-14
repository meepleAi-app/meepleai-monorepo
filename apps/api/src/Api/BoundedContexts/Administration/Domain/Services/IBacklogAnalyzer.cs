using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Analyzes user's game library to detect backlog games (unplayed for 30+ days).
/// </summary>
public interface IBacklogAnalyzer
{
    /// <summary>
    /// Generates backlog alert insights for games not played in extended period.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of backlog alert insights (0-3 alerts)</returns>
    Task<List<AIInsight>> AnalyzeBacklogAsync(Guid userId, CancellationToken cancellationToken = default);
}
