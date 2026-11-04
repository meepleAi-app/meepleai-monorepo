using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Streaming query for generating step-by-step setup guides.
/// Returns progressive events: StateUpdate → SetupStep(s) → Complete
/// AI-03: RAG-based setup guide generation with streaming delivery
/// </summary>
/// <param name="GameId">The game ID to generate setup guide for</param>
/// <param name="UserId">
/// #4206: chi chiede la guida. È obbligatorio perché la guida legge la KB del gioco, e il cancello
/// RAG (ADR-096) decide per utente: senza identità il controllo non può avvenire.
/// </param>
/// <param name="UserRole">Il ruolo con cui il cancello valuta l'accesso (gli admin passano sempre).</param>
/// <param name="PlayerCount">Number of players (0 = generic setup, >0 = adapted to player count)</param>
internal record StreamSetupGuideQuery(
    string GameId,
    Guid UserId,
    string UserRole,
    int PlayerCount = 0
) : IStreamingQuery<RagStreamingEvent>;
