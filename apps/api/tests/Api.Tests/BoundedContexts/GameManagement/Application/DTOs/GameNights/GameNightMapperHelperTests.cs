using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// #2989 inv#17: the upcoming DTO must carry the viewer's RSVP status so the
/// dashboard can render a pending-RSVP card ("Da confermare") for invitees.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightMapperHelperTests
{
    private static GameNightEvent MakeNightWithPendingInvitee(Guid inviteeId)
    {
        var night = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Serata da Marco",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(1));
        night.PreInvite(new List<Guid> { inviteeId });
        return night;
    }

    [Fact]
    public void MapToDto_WithViewerHavingPendingRsvp_SetsViewerRsvpStatusToPending()
    {
        var viewerId = Guid.NewGuid();
        var night = MakeNightWithPendingInvitee(viewerId);

        var dto = GameNightMapperHelper.MapToDto(night, "Marco", viewerId);

        dto.ViewerRsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public void MapToDto_WithoutViewerId_LeavesViewerRsvpStatusNull()
    {
        var night = MakeNightWithPendingInvitee(Guid.NewGuid());

        var dto = GameNightMapperHelper.MapToDto(night, "Marco");

        dto.ViewerRsvpStatus.Should().BeNull();
    }

    [Fact]
    public void MapToDto_WithViewerNotInvited_LeavesViewerRsvpStatusNull()
    {
        var night = MakeNightWithPendingInvitee(Guid.NewGuid());
        var strangerId = Guid.NewGuid();

        var dto = GameNightMapperHelper.MapToDto(night, "Marco", strangerId);

        dto.ViewerRsvpStatus.Should().BeNull();
    }
}
