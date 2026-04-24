using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="MechanicGoldenClaim"/> aggregate (ADR-051 Sprint 1).
/// Provides access to the curated golden-set of mechanic claims used as ground truth for
/// AI comprehension validation scoring.
/// </summary>
public interface IMechanicGoldenClaimRepository
{
    /// <summary>
    /// Returns all golden claims associated with the specified shared game.
    /// </summary>
    Task<IReadOnlyList<MechanicGoldenClaim>> GetByGameAsync(Guid sharedGameId, CancellationToken ct);

    /// <summary>
    /// Returns the golden claim with the specified primary key, or <c>null</c> if not found.
    /// </summary>
    Task<MechanicGoldenClaim?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Stages a new <see cref="MechanicGoldenClaim"/> for insertion.
    /// Persistence occurs at <c>SaveChangesAsync</c>.
    /// </summary>
    Task AddAsync(MechanicGoldenClaim claim, CancellationToken ct);

    /// <summary>
    /// Marks a previously loaded <see cref="MechanicGoldenClaim"/> as modified.
    /// Persistence occurs at <c>SaveChangesAsync</c>.
    /// </summary>
    Task UpdateAsync(MechanicGoldenClaim claim, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="VersionHash"/> representing the current state of the golden-set
    /// for the specified shared game. Used by the scoring pipeline to detect stale cached scores.
    /// </summary>
    Task<VersionHash> GetVersionHashAsync(Guid sharedGameId, CancellationToken ct);
}
