using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Player proposes a score. Host will be notified via SignalR for confirmation.
/// E3-3: Score Proposal Flow.
/// </summary>
public sealed record ProposeScoreCommand(
    Guid SessionId,
    Guid ProposingParticipantId,
    Guid TargetPlayerId,
    int Round,
    string Dimension,
    int Value,
    string? ProposerName) : IRequest;
