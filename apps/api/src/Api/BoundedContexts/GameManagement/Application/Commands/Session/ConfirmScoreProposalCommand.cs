using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Host confirms a score proposal. Score is recorded in the LiveGameSession and broadcast to all participants.
/// E3-3: Score Proposal Flow.
/// </summary>
public sealed record ConfirmScoreProposalCommand(
    Guid SessionId,
    Guid ConfirmingUserId,
    Guid TargetPlayerId,
    int Round,
    string Dimension,
    int Value) : IRequest;
