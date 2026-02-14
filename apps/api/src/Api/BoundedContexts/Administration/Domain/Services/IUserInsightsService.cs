using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Orchestrates AI-powered user insights generation from multiple analyzers.
/// </summary>
public interface IUserInsightsService
{
    /// <summary>
    /// Generates personalized insights for a user by running all analyzers in parallel.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// List of AI insights sorted by priority (descending).
    /// Limited to top 10 insights to avoid UI clutter.
    /// </returns>
    /// <remarks>
    /// Executes backlog, rules, RAG, and streak analyzers concurrently.
    /// Gracefully handles analyzer failures (logs error, continues with other insights).
    /// Performance target: &lt;1s total execution time.
    /// </remarks>
    Task<List<AIInsight>> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
}
