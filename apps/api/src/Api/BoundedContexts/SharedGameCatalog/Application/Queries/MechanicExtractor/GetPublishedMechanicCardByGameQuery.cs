using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Reads the active (non-suppressed) published mechanic card for a game (#528 ME-M1.6). Returns null
/// when the game has no published card or the card was suppressed/taken down — the endpoint maps null
/// to 404 so a suppressed card disappears from the public page automatically.
/// </summary>
internal sealed record GetPublishedMechanicCardByGameQuery(Guid SharedGameId)
    : IRequest<PublishedMechanicCardDto?>;
