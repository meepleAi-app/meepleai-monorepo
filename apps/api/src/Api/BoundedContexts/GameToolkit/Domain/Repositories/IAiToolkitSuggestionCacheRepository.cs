using Api.BoundedContexts.GameToolkit.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Repositories;

/// <summary>
/// Repository contract for <see cref="AiToolkitSuggestionCacheEntry"/>.
/// One row per game — UNIQUE on game_id enforced at DB level.
/// ADR-069 follow-up (#2383).
/// </summary>
internal interface IAiToolkitSuggestionCacheRepository
{
    /// <summary>
    /// Returns the cached suggestion for the given game, or <c>null</c> on cache miss.
    /// </summary>
    Task<AiToolkitSuggestionCacheEntry?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates the cache entry for the entry's game.
    /// Caller must call <c>IUnitOfWork.SaveChangesAsync</c> to persist.
    /// </summary>
    Task UpsertAsync(AiToolkitSuggestionCacheEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Deletes the cached suggestion for the given game.
    /// Idempotent: no-op when no row matches.
    /// Uses <c>ExecuteDeleteAsync</c> — does NOT require a UoW SaveChangesAsync call.
    /// </summary>
    Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default);
}
