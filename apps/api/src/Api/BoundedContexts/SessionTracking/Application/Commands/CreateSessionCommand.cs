using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to create a new session.
/// Session Flow v2.1 — T4: Extended with optional GameNightEventId and GuestNames.
/// When <paramref name="GameNightEventId"/> is null, the handler creates an ad-hoc
/// <c>GameNightEvent</c> implicitly (one-click "Start playing" flow). When provided,
/// the session is attached to the existing InProgress night (at most one active
/// session per night is allowed — enforced by the handler).
/// </summary>
public record CreateSessionCommand(
    Guid UserId,
    Guid GameId,
    string SessionType,
    DateTime? SessionDate,
    string? Location,
    List<ParticipantDto> Participants,
    Guid? GameNightEventId = null,
    IReadOnlyList<string>? GuestNames = null
) : ICommand<CreateSessionResult>;

/// <summary>
/// Result returned by <see cref="CreateSessionCommand"/>.
/// </summary>
/// <param name="SessionId">The new session id.</param>
/// <param name="SessionCode">Human-shareable session code.</param>
/// <param name="Participants">All participants in the session (owner + guests).</param>
/// <param name="GameNightEventId">The game night event id that now owns this session.</param>
/// <param name="GameNightWasCreated">True when an ad-hoc night was created by this command.</param>
/// <param name="AgentDefinitionId">Resolved AgentDefinition for the game (null when not configured).</param>
/// <param name="ToolkitId">Resolved default/user toolkit id for the game (null when not configured).</param>
public record CreateSessionResult(
    Guid SessionId,
    string SessionCode,
    List<ParticipantDto> Participants,
    Guid GameNightEventId,
    bool GameNightWasCreated,
    Guid? AgentDefinitionId,
    Guid? ToolkitId
);
