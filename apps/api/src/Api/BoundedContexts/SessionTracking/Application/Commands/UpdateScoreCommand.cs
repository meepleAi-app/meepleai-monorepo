using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record UpdateScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    int? RoundNumber,
    string? Category,
    decimal ScoreValue
) : IRequest<UpdateScoreResult>;

public record UpdateScoreResult(
    Guid ScoreEntryId,
    decimal NewTotal,
    int NewRank
);
