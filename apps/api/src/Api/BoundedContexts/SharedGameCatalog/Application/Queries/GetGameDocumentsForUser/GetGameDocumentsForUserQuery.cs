using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameDocumentsForUser;

/// <summary>
/// Query to get active documents for a shared game, accessible by authenticated users.
/// Access is granted when:
///   - The game has IsRagPublic = true, OR
///   - The user has the game in their library, OR
///   - The user has declared ownership of the game.
/// Only active (IsActive = true) documents are returned.
/// </summary>
/// <param name="GameId">The ID of the shared game</param>
/// <param name="UserId">The ID of the requesting user</param>
internal record GetGameDocumentsForUserQuery(
    Guid GameId,
    Guid UserId
) : IQuery<IReadOnlyList<SharedGameDocumentDto>>;
