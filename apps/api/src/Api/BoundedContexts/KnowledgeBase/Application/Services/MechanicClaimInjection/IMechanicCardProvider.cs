using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

/// <summary>
/// Best-effort read of the active (Published, non-suppressed) mechanic card for a game, consumed by the
/// RAG agent to inject approved claims into the prompt (spec §6.2). Consumes ONLY the SharedGameCatalog
/// published contract (<c>GetPublishedMechanicCardByGameQuery</c> / <see cref="PublishedMechanicCardDto"/>),
/// never the MechanicAnalysis aggregate. Returns null on absence, suppression, or any failure — the caller
/// falls open to raw RAG.
/// </summary>
internal interface IMechanicCardProvider
{
    Task<PublishedMechanicCardDto?> GetActiveCardAsync(Guid sharedGameId, CancellationToken cancellationToken);
}
