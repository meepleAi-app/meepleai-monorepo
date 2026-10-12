namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Value object defining resource limits for a subscription tier.
/// D3: Game Night Flow - tier system definitions.
/// Issue #4138: MaxAgents (4th factory parameter, after maxPdfSizeBytes) is retired — nothing
/// enforced it once the per-game agent was gone. Its column stays until a later delivery drops it.
/// </summary>
public record TierLimits
{
    public int MaxPrivateGames { get; init; }
    public int MaxPdfUploadsPerMonth { get; init; }
    public long MaxPdfSizeBytes { get; init; }
    public int MaxAgentQueriesPerDay { get; init; }
    public int MaxSessionQueries { get; init; }
    public int MaxSessionPlayers { get; init; }
    public int MaxPhotosPerSession { get; init; }
    public bool SessionSaveEnabled { get; init; }
    public int MaxCatalogProposalsPerWeek { get; init; }
    /// <summary>
    /// Whether the user's tier allows triggering a RAPTOR summary tree rebuild.
    /// Free-tier = false, Premium/Admin = true.
    /// Issue #903: SG2 — KB lifecycle tier gating.
    /// </summary>
    public bool RaptorRebuildEnabled { get; init; }

    /// <summary>
    /// Max gamebook (libro-game) paragraph translations per month. Backs a display-only
    /// quota widget (no enforcement/blocking). Issue #2750 (C14).
    /// Free = 50, Premium = 500, Unlimited = int.MaxValue.
    /// </summary>
    public int MaxGamebookTranslationsPerMonth { get; init; }

    /// <summary>
    /// Max games in the user's library. Issue #4212: the limit moved here from the
    /// <c>LibraryLimits:{tier}:MaxGames</c> configuration keys, which the admin page never wrote
    /// (it saved <c>GameLibrary:*TierLimit</c>, a different key). At least 1: a client that omits
    /// the field sends 0, and a 0 would block every add for the whole tier.
    /// </summary>
    public int MaxLibraryGames { get; init; }

    private TierLimits() { }

    public static TierLimits Create(
        int maxPrivateGames, int maxPdfUploadsPerMonth,
        long maxPdfSizeBytes,
        int maxAgentQueriesPerDay, int maxSessionQueries,
        int maxSessionPlayers, int maxPhotosPerSession,
        bool sessionSaveEnabled, int maxCatalogProposalsPerWeek,
        int maxLibraryGames,
        bool raptorRebuildEnabled = false,
        int maxGamebookTranslationsPerMonth = 0)
    {
        if (maxPrivateGames < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPrivateGames));
        if (maxPdfUploadsPerMonth < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPdfUploadsPerMonth));
        if (maxPdfSizeBytes < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPdfSizeBytes));
        if (maxAgentQueriesPerDay < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxAgentQueriesPerDay));
        if (maxSessionQueries < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxSessionQueries));
        if (maxSessionPlayers < 1)
            throw new ArgumentException("Must be at least 1", nameof(maxSessionPlayers));
        if (maxPhotosPerSession < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPhotosPerSession));
        if (maxCatalogProposalsPerWeek < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxCatalogProposalsPerWeek));
        if (maxGamebookTranslationsPerMonth < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxGamebookTranslationsPerMonth));
        if (maxLibraryGames < 1)
            throw new ArgumentException("Must be at least 1", nameof(maxLibraryGames));

        return new TierLimits
        {
            MaxPrivateGames = maxPrivateGames,
            MaxPdfUploadsPerMonth = maxPdfUploadsPerMonth,
            MaxPdfSizeBytes = maxPdfSizeBytes,
            MaxAgentQueriesPerDay = maxAgentQueriesPerDay,
            MaxSessionQueries = maxSessionQueries,
            MaxSessionPlayers = maxSessionPlayers,
            MaxPhotosPerSession = maxPhotosPerSession,
            SessionSaveEnabled = sessionSaveEnabled,
            MaxCatalogProposalsPerWeek = maxCatalogProposalsPerWeek,
            RaptorRebuildEnabled = raptorRebuildEnabled,
            MaxGamebookTranslationsPerMonth = maxGamebookTranslationsPerMonth,
            MaxLibraryGames = maxLibraryGames
        };
    }

    /// <summary>Unlimited tier for admin users.</summary>
    public static TierLimits Unlimited => Create(
        int.MaxValue, int.MaxValue, 500L * 1024 * 1024,
        int.MaxValue, int.MaxValue,
        12, int.MaxValue, true, int.MaxValue, int.MaxValue, raptorRebuildEnabled: true,
        maxGamebookTranslationsPerMonth: int.MaxValue);

    /// <summary>Free tier defaults.</summary>
    public static TierLimits FreeTier => Create(
        3, 3, 50L * 1024 * 1024, 20, 30, 6, 5, false, 1, 5, raptorRebuildEnabled: false,
        maxGamebookTranslationsPerMonth: 50);

    /// <summary>Premium tier defaults.</summary>
    public static TierLimits PremiumTier => Create(
        15, 15, 200L * 1024 * 1024, 200, 150, 12, 20, true, 5, 50, raptorRebuildEnabled: true,
        maxGamebookTranslationsPerMonth: 500);

    /// <summary>
    /// Normal tier defaults. Issue #4212: until then no "normal" plan existed and normal users
    /// fell back to the free plan for every limit except the library (20 games, kept outside the
    /// plan). This preset is exactly that, so seeding it changes nothing a normal user sees.
    /// </summary>
    public static TierLimits NormalTier => Create(
        3, 3, 50L * 1024 * 1024, 20, 30, 6, 5, false, 1, 20, raptorRebuildEnabled: false,
        maxGamebookTranslationsPerMonth: 50);
}
