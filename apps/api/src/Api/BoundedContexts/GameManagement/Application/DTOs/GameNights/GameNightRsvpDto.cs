using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// DTO representing an RSVP to a game night.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Added UserName for richer client display.
/// </summary>
public sealed record GameNightRsvpDto(
    Guid Id,
    Guid UserId,
    string UserName,
    RsvpStatus Status,
    DateTimeOffset? RespondedAt,
    DateTimeOffset CreatedAt);
