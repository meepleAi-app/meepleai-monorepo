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

    /// <summary>Records that a user performed an action (increments Redis counter).</summary>
    Task RecordUsageAsync(Guid userId, TierAction action, CancellationToken ct = default);

    /// <summary>Gets a snapshot of current usage for a user.</summary>
    Task<UsageSnapshot> GetUsageAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Actions that are subject to tier-based rate limiting.
/// </summary>
public enum TierAction
{
    CreatePrivateGame,
    UploadPdf,
    CreateAgent,
    AgentQuery,
    SessionAgentQuery,
    UploadSessionPhoto,
    SaveSession,
    ProposeToSharedCatalog
}

/// <summary>
/// Snapshot of current resource usage for a user.
/// </summary>
public record UsageSnapshot(
    int PrivateGames, int PrivateGamesMax,
    int PdfThisMonth, int PdfThisMonthMax,
    int AgentQueriesToday, int AgentQueriesTodayMax,
    int SessionQueries, int SessionQueriesMax,
    int Agents, int AgentsMax,
    int PhotosThisSession, int PhotosThisSessionMax,
    bool SessionSaveEnabled,
    int CatalogProposalsThisWeek, int CatalogProposalsThisWeekMax
);
