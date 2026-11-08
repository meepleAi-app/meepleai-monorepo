using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNights;

/// <summary>
/// Tests for <see cref="GameNightMapperHelper"/> — specifically the #3084
/// SessionCount field that drives the dashboard "Recenti" card ("N partite").
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameNightMapperHelperTests
{
    private static GameNightEvent CreatePublishedEvent()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([Guid.NewGuid()]);
        return evt;
    }

    [Fact]
    public void MapToDto_SetsSessionCount_FromAggregateSessions()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");

        var dto = GameNightMapperHelper.MapToDto(evt, "Organizer");

        dto.SessionCount.Should().Be(2);
    }

    [Fact]
    public void MapToDto_SessionCountIsZero_WhenNoSessions()
    {
        var evt = CreatePublishedEvent();

        var dto = GameNightMapperHelper.MapToDto(evt, "Organizer");

        dto.SessionCount.Should().Be(0);
    }

    [Fact]
    public void MapToDto_AddsTheEmailedGuestsToTheMemberCounts()
    {
        // #4706: one accepted member + one pending member (CreatePublishedEvent invites one).
        var evt = CreatePublishedEvent();
        evt.GetRsvp(evt.Rsvps[0].UserId)!.Accept();

        var dto = GameNightMapperHelper.MapToDto(
            evt, "Organizer", guests: new GuestInvitationCounts(Accepted: 2, Pending: 1, Total: 4));

        dto.AcceptedCount.Should().Be(3);
        dto.PendingCount.Should().Be(1);
        dto.TotalInvited.Should().Be(5);
    }

    [Fact]
    public void MapToDto_ADerivedViewerStatus_OutranksTheRsvpRowLookup()
    {
        // #4921: the email guest has no row; the caller derives their state from the invitation and passes it.
        var evt = CreatePublishedEvent();
        var guest = Guid.NewGuid();

        var dto = GameNightMapperHelper.MapToDto(
            evt, "Organizer", guest, viewerRsvpStatus: RsvpStatus.Accepted);

        dto.ViewerRsvpStatus.Should().Be(RsvpStatus.Accepted);
    }

    [Fact]
    public void MapToDto_WithoutADerivedStatus_TheRsvpRowStillDecides()
    {
        var evt = CreatePublishedEvent();
        var member = evt.Rsvps[0].UserId;

        GameNightMapperHelper.MapToDto(evt, "Organizer", member).ViewerRsvpStatus
            .Should().Be(RsvpStatus.Pending);
        GameNightMapperHelper.MapToDto(evt, "Organizer", Guid.NewGuid()).ViewerRsvpStatus
            .Should().BeNull();
    }

    [Fact]
    public void MapToDto_WithoutGuestCounts_KeepsTheMemberOnlyCounts()
    {
        var evt = CreatePublishedEvent();

        var dto = GameNightMapperHelper.MapToDto(evt, "Organizer");

        dto.AcceptedCount.Should().Be(0);
        dto.PendingCount.Should().Be(1);
        dto.TotalInvited.Should().Be(1);
    }
}
