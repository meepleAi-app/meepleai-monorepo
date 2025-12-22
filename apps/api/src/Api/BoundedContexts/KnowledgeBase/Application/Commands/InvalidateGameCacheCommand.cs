using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to invalidate all cached responses for a specific game.
/// Admin-only operation for cache management.
/// Idempotent: succeeds even if game doesn't exist.
/// </summary>
/// <param name="GameId">The game ID to invalidate cache for</param>
internal record InvalidateGameCacheCommand(
    Guid GameId
) : ICommand;
