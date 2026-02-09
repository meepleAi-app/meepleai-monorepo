using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for RuleConflictFAQ aggregate.
/// Issue #3761: Conflict Resolution FAQ System.
/// </summary>
internal interface IRuleConflictFaqRepository
{
    /// <summary>
    /// Finds FAQ resolution for a specific conflict pattern.
    /// </summary>
    Task<RuleConflictFAQ?> FindByPatternAsync(
        Guid gameId,
        string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all FAQ entries for a game, ordered by usage count.
    /// </summary>
    Task<IReadOnlyList<RuleConflictFAQ>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new FAQ entry.
    /// </summary>
    Task AddAsync(RuleConflictFAQ faq, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing FAQ entry.
    /// </summary>
    Task UpdateAsync(RuleConflictFAQ faq, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a FAQ entry.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
