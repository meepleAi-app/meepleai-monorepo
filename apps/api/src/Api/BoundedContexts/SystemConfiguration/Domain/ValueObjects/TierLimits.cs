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

    private TierLimits() { }

    public static TierLimits Create(
        int maxPrivateGames, int maxPdfUploadsPerMonth,
        long maxPdfSizeBytes,
        int maxAgentQueriesPerDay, int maxSessionQueries,
        int maxSessionPlayers, int maxPhotosPerSession,
        bool sessionSaveEnabled, int maxCatalogProposalsPerWeek,
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
            MaxGamebookTranslationsPerMonth = maxGamebookTranslationsPerMonth
        };
    }

    /// <summary>Unlimited tier for admin users.</summary>
    public static TierLimits Unlimited => Create(
        int.MaxValue, int.MaxValue, 500L * 1024 * 1024,
        int.MaxValue, int.MaxValue,
        12, int.MaxValue, true, int.MaxValue, raptorRebuildEnabled: true,
        maxGamebookTranslationsPerMonth: int.MaxValue);

    /// <summary>Free tier defaults.</summary>
    public static TierLimits FreeTier => Create(
        3, 3, 50L * 1024 * 1024, 20, 30, 6, 5, false, 1, raptorRebuildEnabled: false,
        maxGamebookTranslationsPerMonth: 50);

    /// <summary>Premium tier defaults.</summary>
    public static TierLimits PremiumTier => Create(
        15, 15, 200L * 1024 * 1024, 200, 150, 12, 20, true, 5, raptorRebuildEnabled: true,
        maxGamebookTranslationsPerMonth: 500);
}
