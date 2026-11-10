using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4759: who sits at a game night's table, written once. The organizer and the members with an RSVP
/// row are known from the aggregate; whoever accepted an email invitation while logged in has no RSVP
/// row, and the repository answers for them. An empty caller id is nobody.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GameNightParticipationTests
{
    private readonly Mock<IGameNightEventRepository> _repo = new();

    private static GameNightEvent NightOf(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        return evt;
    }

    private Task<bool> IsParticipant(GameNightEvent night, Guid caller) =>
        GameNightParticipation.IsParticipantAsync(night, caller, _repo.Object, TestContext.Current.CancellationToken);

    private void VerifyRepositoryNotAsked() =>
        _repo.Verify(
            r => r.HasAcceptedEmailInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task Organizer_IsAParticipant_WithoutAskingTheRepository()
    {
        var organizer = Guid.NewGuid();

        (await IsParticipant(NightOf(organizer), organizer)).Should().BeTrue();
        VerifyRepositoryNotAsked();
    }

    [Fact]
    public async Task MemberWithAnRsvpRow_IsAParticipant_WithoutAskingTheRepository()
    {
        var member = Guid.NewGuid();

        (await IsParticipant(NightOf(Guid.NewGuid(), member), member)).Should().BeTrue();
        VerifyRepositoryNotAsked();
    }

    [Fact]
    public async Task UserWhoAcceptedByEmail_IsAParticipant_AsTheRepositorySays()
    {
        var guest = Guid.NewGuid();
        var night = NightOf(Guid.NewGuid());
        _repo.Setup(r => r.HasAcceptedEmailInvitationAsync(night.Id, guest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        (await IsParticipant(night, guest)).Should().BeTrue();
    }

    [Fact]
    public async Task Stranger_IsNotAParticipant()
    {
        var night = NightOf(Guid.NewGuid(), Guid.NewGuid());
        _repo.Setup(r => r.HasAcceptedEmailInvitationAsync(night.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        (await IsParticipant(night, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task EmptyCaller_IsNobody_WithoutAskingTheRepository()
    {
        // What an endpoint that forgot to derive the caller would pass; it must not match anything.
        var night = NightOf(Guid.NewGuid());

        (await IsParticipant(night, Guid.Empty)).Should().BeFalse();
        VerifyRepositoryNotAsked();
    }
}
