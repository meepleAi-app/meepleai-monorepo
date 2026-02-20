using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all Knowledge Base cards (indexed vector documents) for a shared game.
/// Optionally filter by indexing status (pending|processing|completed|failed).
/// Issue #4925
/// </summary>
internal record GetSharedGameKbCardsQuery(
    Guid SharedGameId,
    string? Status = null
) : IQuery<IReadOnlyList<KbCardDto>>;
