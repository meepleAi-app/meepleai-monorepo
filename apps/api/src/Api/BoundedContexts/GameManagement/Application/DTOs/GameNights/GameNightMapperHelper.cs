using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Mapper helper for GameNight domain entities to DTOs.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Added OrganizerName, UserName, PendingCount enrichment.
/// </summary>
internal static class GameNightMapperHelper
{
    public static GameNightDto MapToDto(GameNightEvent gameNight, string organizerName)
    {
        ArgumentNullException.ThrowIfNull(gameNight);

        return new GameNightDto(
            Id: gameNight.Id,
            OrganizerId: gameNight.OrganizerId,
            OrganizerName: organizerName,
            Title: gameNight.Title,
            Description: gameNight.Description,
            ScheduledAt: gameNight.ScheduledAt,
            Location: gameNight.Location,
            MaxPlayers: gameNight.MaxPlayers,
            GameIds: gameNight.GameIds,
            Status: gameNight.Status,
            AcceptedCount: gameNight.AcceptedCount,
            PendingCount: gameNight.Rsvps.Count(r => r.Status == RsvpStatus.Pending),
            TotalInvited: gameNight.Rsvps.Count,
            CreatedAt: gameNight.CreatedAt,
            UpdatedAt: gameNight.UpdatedAt);
    }

    public static GameNightRsvpDto MapToRsvpDto(GameNightRsvp rsvp, string userName)
    {
        ArgumentNullException.ThrowIfNull(rsvp);

        return new GameNightRsvpDto(
            Id: rsvp.Id,
            UserId: rsvp.UserId,
            UserName: userName,
            Status: rsvp.Status,
            RespondedAt: rsvp.RespondedAt,
            CreatedAt: rsvp.CreatedAt);
    }
}
