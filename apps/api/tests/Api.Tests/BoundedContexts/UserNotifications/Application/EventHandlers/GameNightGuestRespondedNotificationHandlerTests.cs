using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// #4947: a guest invited by email answers, the organizer hears of it — as they hear of a member's answer. The
/// event names the invitation and the night, not the organizer: the night is read for them.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
[Trait("Issue", "4947")]
public sealed class GameNightGuestRespondedNotificationHandlerTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly GameNightGuestRespondedNotificationHandler _sut;

    public GameNightGuestRespondedNotificationHandlerTests()
    {
        _sut = new GameNightGuestRespondedNotificationHandler(
            _dispatcher.Object, _nights.Object, _invitations.Object, _users.Object,
            NullLogger<GameNightGuestRespondedNotificationHandler>.Instance);
    }

    private GameNightEvent GivenANight()
    {
        var night = GameNightEvent.Create(Guid.NewGuid(), "Friday Catan", UtcNow.AddDays(3));
        _nights.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        return night;
    }

    private GameNightInvitation GivenAnInvitation(GameNightEvent night, GameNightInvitationStatus status, Guid? responder, string? name)
    {
        var invitation = GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: "tokentokentokentokent1",
            gameNightId: night.Id,
            email: "guest@example.com",
            status: status,
            expiresAt: UtcNow.AddDays(7),
            respondedAt: UtcNow,
            respondedByUserId: responder,
            createdAt: UtcNow.AddDays(-1),
            createdBy: night.OrganizerId,
            respondedByName: name);
        _invitations.Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        return invitation;
    }

    private static GameNightInvitationRespondedEvent EventFor(GameNightInvitation invitation, GameNightInvitationStatus status) =>
        new(invitation.Id, invitation.GameNightId, invitation.Token, status, invitation.RespondedByUserId);

    private NotificationMessage Dispatched()
    {
        NotificationMessage? captured = null;
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Invocations.Should().ContainSingle();
        captured = (NotificationMessage)_dispatcher.Invocations.Single().Arguments[0];
        return captured;
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Accepted, "accepted")]
    [InlineData(GameNightInvitationStatus.Declined, "declined")]
    public async Task TheOrganizerIsTold_WhatTheGuestAnswered_UnderTheNameTheGuestLeft(GameNightInvitationStatus answer, string word)
    {
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, answer, responder: null, name: "Marco");

        await _sut.Handle(EventFor(invitation, answer), TestContext.Current.CancellationToken);

        var message = Dispatched();
        message.RecipientUserId.Should().Be(night.OrganizerId);
        message.Type.Should().Be(NotificationType.GameNightRsvpReceived);
        ((GenericPayload)message.Payload).Body.Should().Be($"Marco {word} your game night invitation");
        message.DeepLinkPath.Should().Contain(night.Id.ToString());
    }

    [Fact]
    public async Task TheMessageIsTheOneAMemberProduces_SoTheFeedReadsAsOne()
    {
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Accepted, responder: null, name: "Marco");
        var evt = EventFor(invitation, GameNightInvitationStatus.Accepted);

        await _sut.Handle(evt, TestContext.Current.CancellationToken);

        var expected = GameNightRsvpReceivedMessage.For(night.OrganizerId, night.Id, "Marco", RsvpStatus.Accepted, evt.EventId);
        Dispatched().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task TheEventId_IsTheDedupKey_BecauseTheSameEventReachesTheHandlerMoreThanOnce()
    {
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Accepted, responder: null, name: "Marco");
        var evt = EventFor(invitation, GameNightInvitationStatus.Accepted);

        await _sut.Handle(evt, TestContext.Current.CancellationToken);

        Dispatched().SourceEventId.Should().Be(evt.EventId);
    }

    [Fact]
    public async Task WithoutAName_TheAccountThatAnswered_GivesItsDisplayName()
    {
        var night = GivenANight();
        var responder = Guid.NewGuid();
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Declined, responder, name: null);
        _users.Setup(r => r.GetByIdAsync(responder, It.IsAny<CancellationToken>())).ReturnsAsync(User.CreateForOAuth(
            responder, new Email("guest@example.com"), "Giulia", Role.User, tier: null, oauthProvider: "google",
            timeProvider: TimeProvider.System));

        await _sut.Handle(EventFor(invitation, GameNightInvitationStatus.Declined), TestContext.Current.CancellationToken);

        ((GenericPayload)Dispatched().Payload).Body.Should().StartWith("Giulia declined");
    }

    [Fact]
    public async Task WithNeitherANameNorAnAccount_TheAddressTheOrganizerTyped_NamesTheGuest()
    {
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Accepted, responder: null, name: null);

        await _sut.Handle(EventFor(invitation, GameNightInvitationStatus.Accepted), TestContext.Current.CancellationToken);

        ((GenericPayload)Dispatched().Payload).Body.Should().StartWith("guest@example.com accepted");
    }

    [Fact]
    public async Task TheNightIsGone_NothingIsSent_AndNothingThrows()
    {
        var night = GameNightEvent.Create(Guid.NewGuid(), "Friday Catan", UtcNow.AddDays(3));
        _nights.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Accepted, responder: null, name: "Marco");

        var act = () => _sut.Handle(EventFor(invitation, GameNightInvitationStatus.Accepted), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheDispatcherFails_TheAnswerIsNotFailedWithIt()
    {
        // The event is handled after the answer was saved; throwing would send it back through the outbox to fail the
        // same way, and in Hybrid mode it would surface on the guest's request.
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, GameNightInvitationStatus.Accepted, responder: null, name: "Marco");
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("push gateway down"));

        var act = () => _sut.Handle(EventFor(invitation, GameNightInvitationStatus.Accepted), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Pending)]
    [InlineData(GameNightInvitationStatus.Cancelled)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public async Task AnEventThatIsNotAnAnswer_IsNotTheOrganizersNews(GameNightInvitationStatus notAnAnswer)
    {
        var night = GivenANight();
        var invitation = GivenAnInvitation(night, notAnAnswer, responder: null, name: null);

        await _sut.Handle(EventFor(invitation, notAnAnswer), TestContext.Current.CancellationToken);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _nights.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
