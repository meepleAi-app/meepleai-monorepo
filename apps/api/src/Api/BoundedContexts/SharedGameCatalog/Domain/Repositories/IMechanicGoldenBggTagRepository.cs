using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="MechanicGoldenBggTag"/> entities (ADR-051 Sprint 1).
/// Manages the BGG-sourced mechanic tag set that feeds golden-claim seeding and validation.
/// </summary>
public interface IMechanicGoldenBggTagRepository
{
    /// <summary>
    /// Returns all BGG mechanic tags associated with the specified shared game.
    /// </summary>
    Task<IReadOnlyList<MechanicGoldenBggTag>> GetByGameAsync(Guid sharedGameId, CancellationToken ct);

    /// <summary>
    /// Inserts or updates the full tag batch for the specified shared game in a single operation.
    /// Existing tags not present in <paramref name="tags"/> are left untouched.
    /// </summary>
    Task UpsertBatchAsync(Guid sharedGameId, IReadOnlyList<(string Name, string Category)> tags, CancellationToken ct);

    /// <summary>
    /// Removes the <see cref="MechanicGoldenBggTag"/> with the specified primary key.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
