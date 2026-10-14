using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.SharedKernel.Services;

/// <summary>
/// Service for enforcing tier-based resource limits using Redis atomic counters.
/// D3: Game Night Flow - tier enforcement.
/// </summary>
public interface ITierEnforcementService
{
    /// <summary>Gets the tier limits for a user.</summary>
    Task<TierLimits> GetLimitsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Checks if a user can perform the given action within their tier limits.</summary>
    Task<bool> CanPerformAsync(Guid userId, TierAction action, CancellationToken ct = default);

    /// <summary>
    /// The counter and the limit of a rate-based action, read once. Unlike
    /// <see cref="CanPerformAsync"/> it says how many, and whether the counter could be read at
    /// all: a caller that has to tell "under the limit" from "Redis is down" needs this one.
    /// Issue #4168 (5g-b).
    /// </summary>
    Task<TierUsageCheck> CheckUsageAsync(Guid userId, TierAction action, CancellationToken ct = default);

    /// <summary>Records that a user performed an action (increments Redis counter).</summary>
    Task RecordUsageAsync(Guid userId, TierAction action, CancellationToken ct = default);

    /// <summary>Gets a snapshot of current usage for a user.</summary>
    Task<UsageSnapshot> GetUsageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the monthly gamebook paragraph-translation quota snapshot for display (no enforcement).
    /// Issue #2750 (C14).
    /// </summary>
    Task<GamebookQuotaSnapshot> GetGamebookQuotaSnapshotAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// What <see cref="ITierEnforcementService.CheckUsageAsync"/> found. Issue #4168 (5g-b).
/// </summary>
/// <param name="Current">The counter. Zero when it could not be read.</param>
/// <param name="Max">The limit of the user's tier for the action.</param>
/// <param name="CounterReadable">False when Redis did not answer: the count is unknown, not zero.</param>
public sealed record TierUsageCheck(int Current, int Max, bool CounterReadable)
{
    /// <summary>Admins and editors: no limit, and nothing to count.</summary>
    public bool IsUnlimited => Max == int.MaxValue;

    /// <summary>
    /// The limit is reached. Never true when the counter could not be read: an unknown count
    /// does not stop anybody.
    /// </summary>
    public bool Exceeded => CounterReadable && !IsUnlimited && Current >= Max;
}

/// <summary>
/// Actions that are subject to tier-based rate limiting.
/// </summary>
public enum TierAction
{
    CreatePrivateGame,
    UploadPdf,
    // Issue #4138: CreateAgent stood here. No caller has passed it since #4150, and the
    // per-user agent limit it checked is retired. Redis keys carry the action NAME, not its
    // ordinal, so removing a member does not shift any stored counter.
    AgentQuery,
    SessionAgentQuery,
    UploadSessionPhoto,
    SaveSession,
    ProposeToSharedCatalog,
    /// <summary>
    /// Rebuild the RAPTOR hierarchical summary tree for a game's KB.
    /// Free-tier users are blocked (returns false from CanPerformAsync).
    /// Issue #903: SG2 — KB lifecycle smoke tests.
    /// </summary>
    RaptorRebuild,

    /// <summary>
    /// Translate one gamebook (libro-game) paragraph. Monthly per-user counter backing
    /// a display-only quota widget (no enforcement/blocking). Issue #2750 (C14).
    /// </summary>
    TranslateGamebookParagraph
}

/// <summary>
/// Snapshot of current resource usage for a user.
/// </summary>
public record UsageSnapshot(
    int PrivateGames, int PrivateGamesMax,
    int PdfThisMonth, int PdfThisMonthMax,
    int AgentQueriesToday, int AgentQueriesTodayMax,
    int SessionQueries, int SessionQueriesMax,
    int PhotosThisSession, int PhotosThisSessionMax,
    bool SessionSaveEnabled,
    int CatalogProposalsThisWeek, int CatalogProposalsThisWeekMax
);

/// <summary>
/// Monthly gamebook paragraph-translation quota snapshot (display-only). Issue #2750 (C14).
/// </summary>
public record GamebookQuotaSnapshot(
    int TranslationsThisMonth,
    int MaxTranslationsPerMonth,
    DateTime ResetDate,
    string Tier
);
