namespace Api.Routing;

/// <summary>
/// Request body for joining a session by invite token.
/// </summary>
public sealed record JoinSessionByInviteRequest(string DisplayName);

/// <summary>
/// Request body for joining a session by code.
/// </summary>
public sealed record JoinSessionByCodeRequest(string DisplayName);

/// <summary>
/// Request body for assigning a participant role.
/// </summary>
public sealed record AssignParticipantRoleRequest(Api.BoundedContexts.SessionTracking.Domain.Enums.ParticipantRole NewRole);

/// <summary>Request body for updating a player score.</summary>
public sealed record UpdatePlayerScoreRequest(Guid ParticipantId, decimal ScoreValue, int? RoundNumber = null, string? Category = null);

/// <summary>Request body for rolling dice.</summary>
public sealed record RollSessionDiceRequest(Guid ParticipantId, string Formula, string? Label = null);

/// <summary>Request body for drawing cards from a deck.</summary>
public sealed record DrawSessionCardRequest(Guid ParticipantId, Guid DeckId, int Count = 1);

/// <summary>Request body for controlling the session timer.</summary>
public sealed record SessionTimerActionRequest(
    Api.BoundedContexts.SessionTracking.Application.Commands.TimerAction Action,
    string? ParticipantName = null,
    int? DurationSeconds = null,
    Guid? ParticipantId = null);

/// <summary>Request body for sending a chat action. SenderId is derived from the authenticated user.</summary>
public sealed record SendChatActionRequest(string Content, int? TurnNumber = null, string? MentionsJson = null);

/// <summary>Request body for adding a session event (Issue #276).</summary>
public sealed record AddSessionEventRequest(string EventType, string? Payload = null, string? Source = null);

/// <summary>Request body for requesting an AI-generated turn summary (Issue #277).</summary>
public sealed record TurnSummaryRequest(int? FromPhase = null, int? ToPhase = null, int? LastNEvents = null);

/// <summary>Request body for creating a session checkpoint (Issue #278).</summary>
internal record CreateCheckpointRequest(string Name);
