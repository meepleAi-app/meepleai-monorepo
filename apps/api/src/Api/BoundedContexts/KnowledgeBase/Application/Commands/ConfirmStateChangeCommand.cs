using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to confirm and apply state changes extracted from Ledger Mode parsing.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
/// <param name="SessionId">Game session ID</param>
/// <param name="StateChanges">State changes to apply (JSON dictionary)</param>
/// <param name="UserId">User ID for audit trail</param>
/// <param name="Description">Optional description for the state change snapshot</param>
internal sealed record ConfirmStateChangeCommand(
    Guid SessionId,
    Dictionary<string, object> StateChanges,
    Guid UserId,
    string? Description = null
) : IRequest<MediatR.Unit>;
