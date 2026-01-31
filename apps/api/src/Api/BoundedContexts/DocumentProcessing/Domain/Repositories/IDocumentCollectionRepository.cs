using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for DocumentCollection aggregate.
/// Issue #2051: Multi-document collection persistence
/// </summary>
internal interface IDocumentCollectionRepository : IRepository<DocumentCollection, Guid>
{
    /// <summary>
    /// Finds collection by game ID. Returns null if not found.
    /// </summary>
    Task<DocumentCollection?> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds collections created by a specific user.
    /// </summary>
    Task<IReadOnlyList<DocumentCollection>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a collection exists for a game.
    /// </summary>
    Task<bool> ExistsForGameAsync(Guid gameId, CancellationToken cancellationToken = default);
}
