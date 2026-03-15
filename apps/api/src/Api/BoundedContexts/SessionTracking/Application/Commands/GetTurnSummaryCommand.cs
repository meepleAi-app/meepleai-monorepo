using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to generate an AI-powered summary of session events.
/// Issue #277 - Turn Summary AI Feature
/// </summary>
public record GetTurnSummaryCommand(
    Guid SessionId,
    Guid RequesterId,
    int? FromPhase = null,
    int? ToPhase = null,
    int? LastNEvents = null
) : IRequest<TurnSummaryResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Result of a turn summary generation.
/// </summary>
public record TurnSummaryResult(
    Guid SummaryEventId,
    string Summary,
    int EventsAnalyzed,
    DateTime GeneratedAt
);
