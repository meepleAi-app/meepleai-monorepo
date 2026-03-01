using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record CreateSessionCommand(
    Guid UserId,
    Guid GameId,
    string SessionType,
    DateTime? SessionDate,
    string? Location,
    List<ParticipantDto> Participants
) : ICommand<CreateSessionResult>;

public record CreateSessionResult(
    Guid SessionId,
    string SessionCode,
    List<ParticipantDto> Participants
);
