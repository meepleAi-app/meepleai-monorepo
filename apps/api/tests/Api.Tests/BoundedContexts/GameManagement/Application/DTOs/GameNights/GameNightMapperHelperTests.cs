using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// #2978 (invariante #17): the list/dashboard DTO must carry the viewer's own RSVP status
/// (<c>MyRsvpStatus</c>) so the FE can render the pending-invitee card treatment. The mapper
/// resolves it from the viewer id — null when the viewer is not an invitee (incl. the organizer,
/// who has no RSVP).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GameNightMapperHelperTests
{
    private static GameNightEvent PublishedEventWith(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        return evt;
    }

    [Fact]
    public void MapToDto_ViewerIsPendingInvitee_SetsMyRsvpStatusToPending()
    {
        var invitedUserId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), invitedUserId);

        var dto = GameNightMapperHelper.MapToDto(evt, "Org", invitedUserId);

        dto.MyRsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public void MapToDto_ViewerNotInvited_SetsMyRsvpStatusToNull()
    {
        var evt = PublishedEventWith(Guid.NewGuid(), Guid.NewGuid());

        var dto = GameNightMapperHelper.MapToDto(evt, "Org", viewerUserId: Guid.NewGuid());

        dto.MyRsvpStatus.Should().BeNull();
    }

    [Fact]
    public void MapToDto_ViewerIsOrganizer_SetsMyRsvpStatusToNull()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId, Guid.NewGuid());

        var dto = GameNightMapperHelper.MapToDto(evt, "Org", viewerUserId: organizerId);

        dto.MyRsvpStatus.Should().BeNull();
    }
}
