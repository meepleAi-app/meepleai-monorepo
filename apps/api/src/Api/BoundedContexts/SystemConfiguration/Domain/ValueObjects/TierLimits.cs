namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Value object defining resource limits for a subscription tier.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
public record TierLimits
{
    public int MaxPrivateGames { get; init; }
    public int MaxPdfUploadsPerMonth { get; init; }
    public long MaxPdfSizeBytes { get; init; }
    public int MaxAgents { get; init; }
    public int MaxAgentQueriesPerDay { get; init; }
    public int MaxSessionQueries { get; init; }
    public int MaxSessionPlayers { get; init; }
    public int MaxPhotosPerSession { get; init; }
    public bool SessionSaveEnabled { get; init; }
    public int MaxCatalogProposalsPerWeek { get; init; }

    private TierLimits() { }

    public static TierLimits Create(
        int maxPrivateGames, int maxPdfUploadsPerMonth,
        long maxPdfSizeBytes, int maxAgents,
        int maxAgentQueriesPerDay, int maxSessionQueries,
        int maxSessionPlayers, int maxPhotosPerSession,
        bool sessionSaveEnabled, int maxCatalogProposalsPerWeek)
    {
        if (maxPrivateGames < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPrivateGames));
        if (maxPdfUploadsPerMonth < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPdfUploadsPerMonth));
        if (maxPdfSizeBytes < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxPdfSizeBytes));
        if (maxAgents < 0)
            throw new ArgumentException("Cannot be negative", nameof(maxAgents));
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

        return new TierLimits
        {
            MaxPrivateGames = maxPrivateGames,
            MaxPdfUploadsPerMonth = maxPdfUploadsPerMonth,
            MaxPdfSizeBytes = maxPdfSizeBytes,
            MaxAgents = maxAgents,
            MaxAgentQueriesPerDay = maxAgentQueriesPerDay,
            MaxSessionQueries = maxSessionQueries,
            MaxSessionPlayers = maxSessionPlayers,
            MaxPhotosPerSession = maxPhotosPerSession,
            SessionSaveEnabled = sessionSaveEnabled,
            MaxCatalogProposalsPerWeek = maxCatalogProposalsPerWeek
        };
    }

    /// <summary>Unlimited tier for admin users.</summary>
    public static TierLimits Unlimited => Create(
        int.MaxValue, int.MaxValue, 500L * 1024 * 1024,
        int.MaxValue, int.MaxValue, int.MaxValue,
        12, int.MaxValue, true, int.MaxValue);

    /// <summary>Free tier defaults.</summary>
    public static TierLimits FreeTier => Create(
        3, 3, 50L * 1024 * 1024, 1, 20, 30, 6, 5, false, 1);

    /// <summary>Premium tier defaults.</summary>
    public static TierLimits PremiumTier => Create(
        15, 15, 200L * 1024 * 1024, 10, 200, 150, 12, 20, true, 5);
}
