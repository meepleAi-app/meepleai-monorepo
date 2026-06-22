using Api.SharedKernel.Domain.ValueObjects;

namespace Api.SharedKernel.Application;

/// <summary>
/// Resolves a <see cref="GameRef"/> to its <see cref="GameCoreData"/>.
/// Replaces the legacy <c>IGameRepository.GetByIdOrSharedGameIdAsync</c> dual-fallback pattern (issue #1320).
/// Implementation queries SharedGame (if Kind=Shared) or PrivateGame (if Kind=Private).
/// </summary>
public interface IGameCoreDataProvider
{
    /// <summary>
    /// Resolves a single <see cref="GameRef"/> to its core data, or null if not found.
    /// </summary>
    Task<GameCoreData?> GetCoreDataAsync(GameRef gameRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk fetch for query handlers that need multiple games at once.
    /// Returns dictionary keyed by GameRef; missing entries indicate not-found.
    /// </summary>
    Task<IReadOnlyDictionary<GameRef, GameCoreData>> GetCoreDataBatchAsync(
        IReadOnlyCollection<GameRef> gameRefs,
        CancellationToken cancellationToken = default);
}
