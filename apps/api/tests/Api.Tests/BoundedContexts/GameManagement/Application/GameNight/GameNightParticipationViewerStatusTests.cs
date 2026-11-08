using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4921: the RSVP state a viewer reads as. A member reads their row; an email guest, who has no row,
/// reads the invitation they answered while logged in; the organizer and a stranger read nothing.
/// Before this, the guest read as «not invited / no answer» on a night they had accepted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4921")]
public sealed class GameNightParticipationViewerStatusTests
{
    private readonly Mock<IGameNightEventRepository> _repo = new();

    private static GameNightEvent NightOf(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        return evt;
    }

    private Task<RsvpStatus?> StatusOf(GameNightEvent night, Guid viewer) =>
        GameNightParticipation.ViewerRsvpStatusAsync(night, viewer, _repo.Object, TestContext.Current.CancellationToken);

    private void GuestStatus(GameNightEvent night, Guid viewer, RsvpStatus? status) =>
        _repo.Setup(r => r.GetEmailGuestRsvpStatusesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(night.Id)), viewer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status is { } s
                ? new Dictionary<Guid, RsvpStatus> { [night.Id] = s }
                : new Dictionary<Guid, RsvpStatus>());

    private void VerifyRepositoryNotAsked() =>
        _repo.Verify(
            r => r.GetEmailGuestRsvpStatusesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task Member_ReadsTheirRow_WithoutAskingTheRepository()
    {
        var member = Guid.NewGuid();
        var night = NightOf(Guid.NewGuid(), member);
        night.GetRsvp(member)!.SetMaybe();

        (await StatusOf(night, member)).Should().Be(RsvpStatus.Maybe);
        VerifyRepositoryNotAsked();
    }

    [Theory]
    [InlineData(RsvpStatus.Accepted)]
    [InlineData(RsvpStatus.Declined)]
    public async Task EmailGuest_ReadsTheInvitationTheyAnswered(RsvpStatus answered)
    {
        var guest = Guid.NewGuid();
        var night = NightOf(Guid.NewGuid());
        GuestStatus(night, guest, answered);

        (await StatusOf(night, guest)).Should().Be(answered);
    }

    [Fact]
    public async Task Organizer_ReadsNothing_WithoutAskingTheRepository()
    {
        var organizer = Guid.NewGuid();

        (await StatusOf(NightOf(organizer), organizer)).Should().BeNull();
        VerifyRepositoryNotAsked();
    }

    [Fact]
    public async Task Stranger_ReadsNothing()
    {
        var stranger = Guid.NewGuid();
        var night = NightOf(Guid.NewGuid(), Guid.NewGuid());
        GuestStatus(night, stranger, null);

        (await StatusOf(night, stranger)).Should().BeNull();
    }

    [Fact]
    public async Task EmptyViewer_IsNobody()
    {
        (await StatusOf(NightOf(Guid.NewGuid()), Guid.Empty)).Should().BeNull();
        VerifyRepositoryNotAsked();
    }
}
