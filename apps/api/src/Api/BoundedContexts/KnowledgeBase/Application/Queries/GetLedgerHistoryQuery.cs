using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve ledger state change history for a game session.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
/// <param name="SessionId">Game session ID</param>
/// <param name="Limit">Maximum number of history entries to return (default 50)</param>
internal sealed record GetLedgerHistoryQuery(
    Guid SessionId,
    int Limit = 50
) : IRequest<LedgerHistoryDto>;
