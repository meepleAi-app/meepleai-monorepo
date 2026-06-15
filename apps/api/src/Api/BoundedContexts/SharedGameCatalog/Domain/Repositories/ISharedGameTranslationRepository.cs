using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository for the <see cref="SharedGameTranslation"/> aggregate
/// (issue #2339 — sub-PR 1/3).
/// </summary>
/// <remarks>
/// <para>
/// All reads honour <c>HasQueryFilter(t =&gt; !t.IsDeleted)</c> — soft-deleted
/// rows are transparently filtered out. Admin restore paths that need to
/// fetch a deleted row must compose the query themselves via
/// <c>DbContext.SharedGameTranslations.IgnoreQueryFilters()</c>.
/// </para>
/// <para>
/// Writes are buffered through EF Core's change tracker; the handler is
/// expected to call <c>IUnitOfWork.SaveChangesAsync</c> to flush — failure
/// to do so will leave the translation un-persisted (see ADR-060 §
/// "Persistence rule").
/// </para>
/// <para>
/// Note: interface lives in <c>Domain/Repositories</c> per project DDD
/// convention (CLAUDE.md "Repos: Interfaces in Domain, implementation in
/// Infrastructure"), not <c>Application/Repositories</c> as the original
/// plan suggested.
/// </para>
/// </remarks>
public interface ISharedGameTranslationRepository
{
    /// <summary>Tracks a new translation for insertion on next SaveChanges.</summary>
    Task AddAsync(SharedGameTranslation translation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an existing translation and marks it as Modified so the next
    /// SaveChanges pushes the in-memory state to the row. Includes the
    /// aggregate's <c>Xmin</c> so EF's concurrency token check fires if the
    /// row has been touched by another transaction.
    /// </summary>
    Task UpdateAsync(SharedGameTranslation translation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the active translation for <paramref name="gameId"/> and
    /// <paramref name="locale"/>. Returns <c>null</c> when there is no
    /// active row (either none exists or it has been soft-deleted).
    /// </summary>
    Task<SharedGameTranslation?> GetByGameIdAndLocaleAsync(
        Guid gameId,
        string locale,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every active translation for the game.</summary>
    Task<IReadOnlyList<SharedGameTranslation>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batched lookup used by <c>GameTitleResolver</c> to enrich the
    /// catalog/library list endpoints with one round-trip. Result groups
    /// translations by their parent <see cref="SharedGameTranslation.SharedGameId"/>
    /// and omits soft-deleted rows.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<SharedGameTranslation>>>
        GetByGameIdsAsync(
            IReadOnlyList<Guid> gameIds,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when an active (non-soft-deleted) translation
    /// already exists for <paramref name="gameId"/>+<paramref name="locale"/>.
    /// Used by the Add validator to short-circuit before the DB unique
    /// constraint fires.
    /// </summary>
    Task<bool> ExistsActiveAsync(
        Guid gameId,
        string locale,
        CancellationToken cancellationToken = default);
}
