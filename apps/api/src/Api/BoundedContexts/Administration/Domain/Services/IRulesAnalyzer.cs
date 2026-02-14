using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Analyzes user's saved rulebook PDFs to detect unreviewed rules (7+ days).
/// </summary>
public interface IRulesAnalyzer
{
    /// <summary>
    /// Generates rules reminder insights for PDFs not accessed recently.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of rules reminder insights (0-3 reminders)</returns>
    Task<List<AIInsight>> AnalyzeRulebooksAsync(Guid userId, CancellationToken cancellationToken = default);
}
