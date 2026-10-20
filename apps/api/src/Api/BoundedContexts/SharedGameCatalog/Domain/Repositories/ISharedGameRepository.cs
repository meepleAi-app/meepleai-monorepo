using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for SharedGame aggregate persistence.
/// </summary>
public interface ISharedGameRepository
{
    /// <summary>
    /// Adds a new shared game to the repository.
    /// </summary>
    /// <param name="sharedGame">The game to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #3153 — adds a new shared game together with its designer/publisher
    /// M:N links, resolved get-or-create BY NAME (case-insensitively) from the raw
    /// provenance names. Names are trimmed; empty / &gt;200-char entries are skipped
    /// (never thrown); existing shared designer/publisher rows are reused. Persistence
    /// is deferred to the caller's unit of work (no SaveChanges inside).
    /// </summary>
    /// <param name="sharedGame">The game to add (scalars only; its own designer/publisher collections are ignored).</param>
    /// <param name="designerNames">Raw designer names to resolve get-or-create.</param>
    /// <param name="publisherNames">Raw publisher names to resolve get-or-create.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(
        SharedGame sharedGame,
        IReadOnlyList<string> designerNames,
        IReadOnlyList<string> publisherNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by its ID.
    /// </summary>
    /// <param name="id">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a non-deleted shared game exists for the given ID.
    /// Issue #2552: lightweight existence probe (no aggregate hydration) used by the
    /// companion-saga path to reject a valid-but-nonexistent GameId with a 404 instead of
    /// letting the FK violation surface as a 500 at SaveChanges.
    /// </summary>
    /// <param name="id">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a non-deleted game with that ID exists, false otherwise</returns>
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple shared games by their IDs in a single batch query.
    /// Issue #3663: Added to prevent N+1 queries in GetUserLibraryQueryHandler.
    /// </summary>
    /// <param name="ids">The game IDs to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping game ID to game (only includes found games)</returns>
    Task<IReadOnlyDictionary<Guid, SharedGame>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a lightweight title-only lookup for the given game IDs.
    /// Issue #660: Used by GetAllAgentsQueryHandler to populate AgentDto.GameName without
    /// hydrating full SharedGame aggregates (avoids unnecessary domain reconstitution overhead).
    /// </summary>
    /// <param name="ids">The game IDs to retrieve names for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping game ID to title (only includes found, non-deleted games)</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by its BoardGameGeek ID.
    /// </summary>
    /// <param name="bggId">The BGG ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetByBggIdAsync(int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing shared game.
    /// </summary>
    /// <param name="sharedGame">The game to update</param>
    void Update(SharedGame sharedGame);

    /// <summary>
    /// Epic #3470 — reconciles the game's per-context cover assignment child
    /// collection against its DB-resident rows: children new since the load are
    /// inserted, existing ones updated in place (preserving the loaded <c>xmin</c>
    /// concurrency token), and rows the aggregate dropped are deleted. Does NOT
    /// call <c>SaveChanges</c>; the caller's unit of work commits. This is the
    /// child-safe alternative to a detached full-graph <c>Update()</c>, which would
    /// silently lose a newly-added assignment on Postgres.
    /// </summary>
    /// <param name="sharedGame">The aggregate whose <c>CoverAssignments</c> are the desired state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReconcileCoverAssignmentsAsync(SharedGame sharedGame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #3615 — clears the rendered per-context crops pinned to <paramref name="source"/> for
    /// one game, because the base image they were derived from has just been replaced.
    ///
    /// <para>
    /// A targeted UPDATE rather than <see cref="ReconcileCoverAssignmentsAsync"/>: reconciliation
    /// treats the in-memory collection as the desired state, so calling it from a handler that
    /// loaded the aggregate WITHOUT its assignments would delete every row. The cover-regeneration
    /// paths do not all load them, and a silent data loss is a far worse failure than a stale crop.
    /// </para>
    /// <para>
    /// Does NOT call <c>SaveChanges</c> — it executes immediately, server-side, and bypasses the
    /// change tracker, so an entity already tracked in this context keeps its stale in-memory value
    /// (harmless in these write-then-finish paths; the aggregate mirrors the same invalidation
    /// in-memory anyway).
    /// </para>
    /// </summary>
    /// <returns>Number of assignments whose crop was cleared.</returns>
    Task<int> InvalidateGeneratedCropsAsync(
        Guid gameId,
        CoverAssignmentSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a game with the given BGG ID already exists.
    /// </summary>
    /// <param name="bggId">The BGG ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsByBggIdAsync(int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by a FAQ ID contained within it.
    /// </summary>
    /// <param name="faqId">The FAQ ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default);

    /// <summary>
    /// #4557 — adds one vote to a FAQ and returns the new count, without loading or saving the
    /// <c>SharedGame</c> aggregate.
    ///
    /// <para>
    /// A vote is a counter on <c>game_faqs</c>, not a change to the game. It is public and
    /// anonymous, so it must not go through <see cref="Update"/>: that rewrites the game row and takes
    /// its <c>xmin</c> check, so two simultaneous votes would end in a 409. (Before #4585 it also
    /// zeroed <c>has_knowledge_base</c>, <c>is_rag_public</c>, <c>bgg_raw_data</c> and
    /// <c>test_run_id</c>, which the mapper did not carry.)
    /// </para>
    /// <para>
    /// Executes immediately, server-side, as <c>upvote_count = upvote_count + 1</c>: concurrent votes
    /// are serialised by Postgres and all counted. Does NOT call <c>SaveChanges</c> and bypasses the
    /// change tracker. A FAQ of a soft-deleted game is not found.
    /// </para>
    /// </summary>
    /// <param name="faqId">The FAQ ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The new upvote count, or null when the FAQ does not exist or its game is deleted.</returns>
    Task<int?> IncrementFaqUpvoteAsync(Guid faqId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by an Errata ID contained within it.
    /// </summary>
    /// <param name="errataId">The Errata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetGameByErrataIdAsync(Guid errataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by its ID, including soft-deleted games.
    /// </summary>
    /// <param name="id">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetByIdWithDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a game with the exact title (case-insensitive) already exists.
    /// Used for duplicate detection during Excel import.
    /// </summary>
    Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shared games with the specified GameDataStatus.
    /// Used for querying skeleton games for bulk enrichment.
    /// </summary>
    Task<List<SharedGame>> GetByGameDataStatusAsync(GameDataStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of non-soft-deleted shared games (#1861: catalog cumulative stats).
    /// </summary>
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
}
