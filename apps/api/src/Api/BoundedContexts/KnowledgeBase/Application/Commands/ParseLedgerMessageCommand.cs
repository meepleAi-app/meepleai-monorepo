using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to parse a chat message for game state changes using Ledger Mode NLP.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
/// <param name="SessionId">Game session ID</param>
/// <param name="Message">Chat message to parse</param>
/// <param name="UserId">User ID for audit trail</param>
internal sealed record ParseLedgerMessageCommand(
    Guid SessionId,
    string Message,
    Guid UserId
) : IRequest<LedgerParseResultDto>;
