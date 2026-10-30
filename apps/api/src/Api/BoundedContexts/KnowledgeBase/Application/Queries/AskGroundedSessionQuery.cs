using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #3390 Slice 2 — grounded, non-streaming RAG answer for the in-session agent.
///
/// KnowledgeBase owns "grounded answer on the rulebook"; SessionTracking (image/text agent
/// path) consumes this capability via <c>IMediator.Send</c> (same cross-BC pattern as
/// <c>CreateSessionCommandHandler</c> → <c>GetKbReadinessQuery</c>), never by injecting a KB
/// service directly (CLAUDE.md DDD rule: "Direct service injection (use MediatR)").
///
/// The turn TEXT (<see cref="Question"/>) is the retrieval query; the vision output
/// (<see cref="GameStateContext"/>, already distilled to JSON by SessionTracking's
/// GameStateExtractor) is injected as STATE CONTEXT into the generation prompt — it does NOT
/// bypass the rulebook. Retrieval runs under <c>RetrievalPolicy.LiveSession</c> (enhancements
/// OFF, CRAG web-fallback off) exactly like the SSE text path.
/// </summary>
/// <param name="GameId">Game whose indexed rulebook is retrieved against.</param>
/// <param name="Question">The turn text — used as the retrieval query. Must be non-empty
/// (the empty-text → vision-derived-query case is #3390 Slice 3).</param>
/// <param name="GameStateContext">Optional distilled board state (vision JSON) injected as
/// prompt context. Null when no readable snapshot exists.</param>
/// <param name="UserId">Resolved user id for copyright-tier gating of citations. Guest →
/// <c>Guid.Empty</c> (most restrictive tier).</param>
/// <param name="AgentLanguage">ISO 639-1 language for generation + copyright fallback copy.</param>
/// <param name="ResponseInstructions">Optional instructions for the generation step only (persona and
/// answer format, e.g. the Arbitro's VERDETTO/NOTA), appended to the system prompt. They never reach
/// retrieval: <see cref="Question"/> is also the retrieval query, and a format pasted into it would be
/// embedded and matched against the rulebook (#4751).</param>
internal sealed record AskGroundedSessionQuery(
    Guid GameId,
    string Question,
    string? GameStateContext,
    Guid UserId,
    string AgentLanguage = "en",
    string? ResponseInstructions = null) : IQuery<GroundedSessionAnswerDto>;
