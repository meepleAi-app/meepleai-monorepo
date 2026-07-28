using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record UpdateScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    int? RoundNumber,
    string? Category,
    decimal ScoreValue,
    Guid RequestedBy // IDOR guard: authenticated caller — must be the session owner or a participant.
) : IRequest<UpdateScoreResult>;

public record UpdateScoreResult(
    Guid ScoreEntryId,
    decimal NewTotal,
    int NewRank
);
