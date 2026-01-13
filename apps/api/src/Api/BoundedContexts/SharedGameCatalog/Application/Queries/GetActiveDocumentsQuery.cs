using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all active documents for a shared game.
/// Returns one active document per type (Rulebook, Errata, Homerule).
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
internal record GetActiveDocumentsQuery(
    Guid SharedGameId
) : IQuery<IReadOnlyList<SharedGameDocumentDto>>;
