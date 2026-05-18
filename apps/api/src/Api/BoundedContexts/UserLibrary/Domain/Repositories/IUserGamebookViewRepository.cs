namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Read-side repository for the `/api/v1/gamebooks` index view (Issue #1288).
///
/// Aggregates UserLibraryEntries (with SharedGameId OR PrivateGameId) joined
/// with cross-BC signals (GamebookCampaignSessions presence) into a flat
/// projection optimized for the gamebook index card grid.
///
/// Dedicated repository per Interface Segregation Principle — keeps
/// IUserLibraryRepository focused on UserLibraryEntry aggregate operations
/// and avoids cross-BC composition leakage into the main library repo.
///
/// MVP scope (Issue #1288, Phase 1):
///   Returns one projection per UserLibraryEntry that has either:
///     - An active GamebookCampaignSession (cross-schema JOIN), OR
///     - A PrivateGameId (private PDF uploaded)
///
///   Filter rationale: a "gamebook" is operationally defined as a library
///   entry that the user has set up for libro-game play, signalled by either
///   a campaign creation or a private rulebook upload.
///
/// Out of scope (deferred to Bug 2):
///   - Aggregate counters (chunks, sessionsCount, kbStatus) — composition
///     across DocumentProcessing + KnowledgeBase + SessionTracking remains
///     in the query handler via dedicated stats ports.
/// </summary>
internal interface IUserGamebookViewRepository
{
    /// <summary>
    /// Gets the gamebook projection for the given user.
    /// Returns entries with at least one of:
    ///   - active GamebookCampaignSession (not deleted), OR
    ///   - PrivateGameId set (private game library entry).
    /// </summary>
    /// <param name="userId">The owner user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of gamebook view items ordered by most recent activity</returns>
    Task<IReadOnlyList<UserGamebookViewItem>> GetGamebookEntriesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only projection for the gamebook index view.
///
/// Sources:
///   - <see cref="LibraryEntryId"/>: UserLibraryEntries.Id (the gamebook identity)
///   - <see cref="GameId"/>: SharedGameId OR PrivateGameId (XOR per UserLibraryEntry invariant)
///   - <see cref="Title"/>: SharedGames.Title OR PrivateGames.Title (whichever side is set)
///   - <see cref="Year"/>: SharedGames.YearPublished OR PrivateGames.YearPublished
///   - <see cref="Cover"/>: SharedGames.ImageUrl OR PrivateGames.ImageUrl
///   - <see cref="HasActiveCampaign"/>: EXISTS gamebook_campaign_sessions WHERE owner_user_id = userId AND game_id = SharedGameId AND NOT is_deleted
///   - <see cref="HasPrivatePdf"/>: PrivateGameId NOT NULL (the library entry references a private game)
///   - <see cref="LastActivityAt"/>: MAX(library_entry.AddedAt, campaign.updated_at) for ordering
/// </summary>
internal sealed record UserGamebookViewItem(
    Guid LibraryEntryId,
    Guid GameId,
    string Title,
    int? Year,
    string? Cover,
    bool HasActiveCampaign,
    bool HasPrivatePdf,
    DateTime LastActivityAt);
