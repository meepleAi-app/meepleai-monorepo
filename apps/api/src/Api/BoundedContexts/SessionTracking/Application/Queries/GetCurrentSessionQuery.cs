using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T4.
/// Returns the caller's latest Active or Paused session for orphan recovery
/// after page reload / crash (NFR-9). Returns <c>null</c> when no such session exists.
/// </summary>
public record GetCurrentSessionQuery(Guid UserId) : IQuery<CurrentSessionDto?>;

/// <summary>
/// Lightweight DTO for the current-session probe. Carries only the fields
/// the client needs to resume or display the session.
/// </summary>
public record CurrentSessionDto(
    Guid SessionId,
    Guid GameId,
    string Status,
    string SessionCode,
    DateTime SessionDate,
    DateTime? UpdatedAt,
    Guid? GameNightEventId);
