using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// DTO representing a game night event.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Added OrganizerName, PendingCount for richer client display.
/// </summary>
public sealed record GameNightDto(
    Guid Id,
    Guid OrganizerId,
    string OrganizerName,
    string Title,
    string? Description,
    DateTimeOffset ScheduledAt,
    string? Location,
    int? MaxPlayers,
    List<Guid> GameIds,
    GameNightStatus Status,
    int AcceptedCount,
    int PendingCount,
    int TotalInvited,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
