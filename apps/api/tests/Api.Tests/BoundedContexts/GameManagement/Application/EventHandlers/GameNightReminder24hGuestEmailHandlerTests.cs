using System.Diagnostics.Metrics;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4846: the 24 hour reminder, to the guests who were invited BY ADDRESS. The job (<c>GameNightReminderJob</c>)
/// already emits <see cref="GameNightReminder24hEvent"/> once per night and the registered users are reminded by
/// <c>GameNightReminder24hNotificationHandler</c>; an address is not an account and nothing else reaches it.
/// </summary>
/// <remarks>
/// Given a night at 20:00 tomorrow and a guest who accepted, when it is 20:00 today the guest gets the reminder ONCE,
/// even if the event is delivered twice (the job publishes it directly and stamps the night afterwards, so a gap
/// between the two, or another handler of the event throwing, runs it again). The mark on the invitation is what
/// makes it once.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightReminder24hGuestEmailHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);
    private const string BaseUrl = "https://meepleai.test";

    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGameNightEmailService> _email = new();
    private readonly Mock<IGameNightGuestEmailOptOutRepository> _optOuts = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<ILogger<GameNightReminder24hGuestEmailHandler>> _logger = new();

    private GameNightReminder24hGuestEmailHandler CreateHandler() =>
        new(
            _invitations.Object, _nights.Object, _users.Object, _email.Object, _optOuts.Object,
            PublicBaseUrlTestSupport.ProviderFor(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = BaseUrl })
                .Build()),
            _time, _logger.Object);

    private static GameNightInvitation Invitation(
        string email,
        GameNightInvitationStatus status,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? remindedAt = null,
        bool withoutKey = false) =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Guid.NewGuid().ToString("N")[..22],
            gameNightId: Guid.NewGuid(),
            email: email,
            status: status,
            expiresAt: expiresAt ?? Now.AddDays(3),
            respondedAt: null,
            respondedByUserId: null,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid(),
            unsubscribeKey: withoutKey ? null : Guid.NewGuid().ToString("N")[..22],
            reminder24hSentAt: remindedAt);

    private static GameNightEvent PublishedNight(Guid organizerId, string title = "Friday Catan", string? zone = null)
    {
        var night = GameNightEvent.Create(
            organizerId: organizerId,
            title: title,
            scheduledAt: Now.AddHours(24),
            location: "Casa mia",
            timeZoneId: zone);
        night.Publish([]);
        return night;
    }

    private static GameNightReminder24hEvent Reminder(GameNightEvent night) =>
        new(night.Id, night.Title, night.ScheduledAt, night.Location);

    private void Given(GameNightEvent night, params GameNightInvitation[] invitations)
    {
        _nights
            .Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _invitations
            .Setup(r => r.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitations);

        // What the repository does with a mark: it lands on the row, and a later read finds it. (The handler no longer
        // touches the aggregate it read: #4744.)
        _invitations
            .Setup(r => r.MarkReminder24hSentAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback((Guid id, DateTimeOffset at, CancellationToken _) =>
                invitations.Single(i => i.Id == id).MarkReminder24hSent(at))
            .Returns(Task.CompletedTask);
    }

    private void VerifyMarked(GameNightInvitation invitation, Times times) =>
        _invitations.Verify(
            r => r.MarkReminder24hSentAsync(invitation.Id, Now, It.IsAny<CancellationToken>()),
            times);

    private void VerifyNothingMarked() =>
        _invitations.Verify(
            r => r.MarkReminder24hSentAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifySentTo(string address, Times times) =>
        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                address,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(),
                It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public async Task Handle_RemindsEachAcceptedAndEachLivePendingInvitation_AndNoOneElse()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night,
            Invitation("accepted@example.com", GameNightInvitationStatus.Accepted),
            Invitation("pending@example.com", GameNightInvitationStatus.Pending),
            Invitation("declined@example.com", GameNightInvitationStatus.Declined),
            Invitation("withdrawn@example.com", GameNightInvitationStatus.Cancelled),
            Invitation("expired@example.com", GameNightInvitationStatus.Expired),
            Invitation("lapsed@example.com", GameNightInvitationStatus.Pending, expiresAt: Now.AddMinutes(-1)));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        VerifySentTo("accepted@example.com", Times.Once());
        VerifySentTo("pending@example.com", Times.Once());
        VerifySentTo("declined@example.com", Times.Never());
        VerifySentTo("withdrawn@example.com", Times.Never());
        VerifySentTo("expired@example.com", Times.Never());
        VerifySentTo("lapsed@example.com", Times.Never());
    }

    [Fact]
    public async Task Handle_AGuestWhoAccepted_IsToldTheNightIsTomorrow_AGuestWhoHasNotAnswered_IsAskedToAnswer()
    {
        var night = PublishedNight(Guid.NewGuid());
        var accepted = Invitation("accepted@example.com", GameNightInvitationStatus.Accepted);
        var pending = Invitation("pending@example.com", GameNightInvitationStatus.Pending);
        Given(night, accepted, pending);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "accepted@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), false,
                $"{BaseUrl}/invites/{accepted.Token}", $"{BaseUrl}/invites/unsubscribe/{accepted.UnsubscribeKey}",
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "pending@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), true,
                $"{BaseUrl}/invites/{pending.Token}", $"{BaseUrl}/invites/unsubscribe/{pending.UnsubscribeKey}",
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TheEmailCarriesTheOrganizersNameAndLanguage_AndTheNightsTitleTimeLocationAndZone()
    {
        var organizer = new Api.Tests.BoundedContexts.Authentication.TestHelpers.UserBuilder().Build();
        organizer.UpdatePreferences("it", "system", emailNotifications: true, dataRetentionDays: 90);
        _users
            .Setup(r => r.GetByIdAsync(organizer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(organizer);
        var night = PublishedNight(organizer.Id, title: "Venerdì Catan", zone: "Europe/Rome");
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "guest@example.com", organizer.DisplayName, "Venerdì Catan", night.ScheduledAt, "Casa mia",
                It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                new GameNightEmailLocale("it", "Europe/Rome"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrganizerGone_StillRemindsWithTheFallbackNameOfTheInvitation()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "guest@example.com", "A friend", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ConfirmedAttendees_CountsTheGuestsWhoAccepted_OncePerAddress_AndTheRegisteredOnes()
    {
        var member = Guid.NewGuid();
        var night = GameNightEvent.Create(
            organizerId: Guid.NewGuid(), title: "Friday Catan", scheduledAt: Now.AddHours(24));
        night.Publish([member]);
        night.GetRsvp(member)!.Accept();
        Given(night,
            Invitation("one@example.com", GameNightInvitationStatus.Accepted),
            Invitation("One@Example.com", GameNightInvitationStatus.Accepted), // the same person, invited twice
            Invitation("two@example.com", GameNightInvitationStatus.Accepted),
            Invitation("waiting@example.com", GameNightInvitationStatus.Pending));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "waiting@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), 3, It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TheOptOutLink_CarriesTheUnsubscribeKey_NeverTheRsvpToken()
    {
        // The organizer holds the RSVP token; whoever holds the opt-out credential silences the address for every
        // organizer. The reminder writes the key, and the token only in the link that answers the invitation.
        var night = PublishedNight(Guid.NewGuid());
        var invitation = Invitation("guest@example.com", GameNightInvitationStatus.Pending);
        Given(night, invitation);
        string? unsubscribeUrl = null;
        _email
            .Setup(e => e.SendGameNightReminder24hEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTimeOffset, string?, int, bool, string, string, GameNightEmailLocale, CancellationToken>(
                (_, _, _, _, _, _, _, _, unsub, _, _) => unsubscribeUrl = unsub)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        unsubscribeUrl.Should().EndWith("/" + invitation.UnsubscribeKey);
        unsubscribeUrl.Should().NotContain(invitation.Token);
    }

    [Fact]
    public async Task Handle_AnAddressWhoseInvitationsPredateTheKey_IsNeitherWrittenToNorMarked_AndTheOthersAre()
    {
        var night = PublishedNight(Guid.NewGuid());
        var legacy = Invitation("legacy@example.com", GameNightInvitationStatus.Accepted, withoutKey: true);
        Given(night, legacy, Invitation("fine@example.com", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "legacy@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "fine@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
        legacy.Reminder24hSentAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TheSameAddressTwice_RemindsOnce_AndMarksBothInvitations()
    {
        var night = PublishedNight(Guid.NewGuid());
        var first = Invitation("guest@example.com", GameNightInvitationStatus.Accepted);
        var second = Invitation("Guest@Example.com", GameNightInvitationStatus.Pending);
        Given(night, first, second);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        // They said yes with one of the two invitations: they are not asked to answer again, and the link is the
        // accepted one.
        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "guest@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), false,
                $"{BaseUrl}/invites/{first.Token}", $"{BaseUrl}/invites/unsubscribe/{first.UnsubscribeKey}",
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
        first.Reminder24hSentAt.Should().Be(Now);
        second.Reminder24hSentAt.Should().Be(Now);
        VerifyMarked(first, Times.Once());
        VerifyMarked(second, Times.Once());
    }

    [Fact]
    public async Task Handle_TheEventIsDeliveredTwice_TheSecondDeliveryWritesNothing()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night,
            Invitation("first@example.com", GameNightInvitationStatus.Accepted),
            Invitation("second@example.com", GameNightInvitationStatus.Pending));
        var handler = CreateHandler();

        await handler.Handle(Reminder(night), CancellationToken.None);
        await handler.Handle(Reminder(night), CancellationToken.None);

        _email.Invocations.Should().HaveCount(2, "SMTP has no remote dedup: one email per address in all");
        VerifySentTo("first@example.com", Times.Once());
        VerifySentTo("second@example.com", Times.Once());
        // What the second delivery relies on is the WRITTEN mark: one write per address in the first delivery, none
        // in the second.
        _invitations.Verify(
            r => r.MarkReminder24hSentAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_TheMarkIsAWriteOfThatColumnOnly_SoAnAnswerThatArrivedMeanwhileIsNotUndone()
    {
        // #4744. The list was read before the first email went out: if the guest accepted (or the organizer withdrew)
        // in the meantime, rewriting that copy whole would put the old state back. The mark is a write of its own
        // column, so there is no copy to write back: the aggregate is never handed to UpdateAsync.
        var night = PublishedNight(Guid.NewGuid());
        var stale = Invitation("guest@example.com", GameNightInvitationStatus.Pending);
        Given(night, stale);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        VerifyMarked(stale, Times.Once());
        _invitations.Verify(
            r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _invitations.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the mark does not need the row again: the repository writes the column and nothing else");
    }

    [Fact]
    public async Task Handle_ADraftNight_SendsNoReminder()
    {
        // Only a Published night is on: a draft is not (the status test is «is published», not «is not cancelled»).
        var draft = GameNightEvent.Create(
            organizerId: Guid.NewGuid(), title: "Friday Catan", scheduledAt: Now.AddHours(24));
        Given(draft, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(Reminder(draft), CancellationToken.None);

        _email.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_APendingInvitationThatExpiresRightNow_IsNotReminded()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night, Invitation("late@example.com", GameNightInvitationStatus.Pending, expiresAt: Now));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        VerifySentTo("late@example.com", Times.Never());
    }

    [Fact]
    public async Task Handle_ConfirmedAttendees_StillCountsAGuestWhoWasRemindedAlready()
    {
        // The second delivery: «one» has the mark, «waiting» does not. The count is of who said yes, not of who is
        // being written to now.
        var night = PublishedNight(Guid.NewGuid());
        Given(night,
            Invitation("one@example.com", GameNightInvitationStatus.Accepted, remindedAt: Now.AddMinutes(-5)),
            Invitation("two@example.com", GameNightInvitationStatus.Accepted),
            Invitation("waiting@example.com", GameNightInvitationStatus.Pending));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Verify(
            e => e.SendGameNightReminder24hEmailAsync(
                "waiting@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), 2, It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AnAddressAlreadyRemindedThroughAnotherInvitation_IsNotRemindedAgain()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night,
            Invitation("guest@example.com", GameNightInvitationStatus.Accepted, remindedAt: Now.AddMinutes(-1)),
            Invitation("guest@example.com", GameNightInvitationStatus.Pending));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Invocations.Should().BeEmpty();
        VerifyNothingMarked();
    }

    [Fact]
    public async Task Handle_AnAddressThatOptedOut_IsSkippedAndGetsNoMark_TheOthersAreReminded()
    {
        var night = PublishedNight(Guid.NewGuid());
        var optedOut = Invitation("stopped@example.com", GameNightInvitationStatus.Accepted);
        Given(night, optedOut, Invitation("other@example.com", GameNightInvitationStatus.Accepted));
        _optOuts
            .Setup(r => r.ExistsAsync("stopped@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        VerifySentTo("stopped@example.com", Times.Never());
        VerifySentTo("other@example.com", Times.Once());
        optedOut.Reminder24hSentAt.Should().BeNull("the mark says «written to», which this address was not");
    }

    [Theory]
    [InlineData(GameNightStatus.Cancelled)]
    [InlineData(GameNightStatus.Completed)]
    public async Task Handle_ANightThatIsNoLongerOn_SendsNoReminder(GameNightStatus status)
    {
        // The event was emitted while the night was published; by the time the handler runs (the outbox can be
        // seconds or minutes late) the organizer may have cancelled it. A reminder for a cancelled night, with
        // the cancellation already sent, would contradict it.
        var night = PublishedNight(Guid.NewGuid());
        if (status == GameNightStatus.Cancelled)
            night.Cancel();
        else
            night.Complete();
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ANightThatIsNotFound_SendsNothing()
    {
        var night = PublishedNight(Guid.NewGuid());
        _nights
            .Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);
        using var failures = new DispatchFailureCapture();

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _email.Invocations.Should().BeEmpty();
        // Not a failure that the catch-all swallowed (a missing check would end in a NullReferenceException, which
        // is counted): it is a night that is no longer there, and the handler says so and goes.
        failures.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ANightWithNoGuestsByAddress_DoesNotEvenLookUpTheOrganizer()
    {
        var night = PublishedNight(Guid.NewGuid());
        Given(night);

        await CreateHandler().Handle(Reminder(night), CancellationToken.None);

        _users.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _email.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OneSendFails_TheOthersGoOut_NothingIsThrown_TheFailureIsCounted_AndLeavesNoMark()
    {
        var night = PublishedNight(Guid.NewGuid());
        var refused = Invitation("first@example.com", GameNightInvitationStatus.Accepted);
        Given(night, refused, Invitation("second@example.com", GameNightInvitationStatus.Accepted));
        _email
            .Setup(e => e.SendGameNightReminder24hEmailAsync(
                "first@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp refused"));
        using var failures = new DispatchFailureCapture();

        var act = () => CreateHandler().Handle(Reminder(night), CancellationToken.None);

        // MediatR runs the handlers of an event in sequence: a throw would stop the ones after this, among them the
        // in-app reminder for the registered users. It is counted instead, under this handler's name.
        await act.Should().NotThrowAsync();
        VerifySentTo("second@example.com", Times.Once());
        failures.Count.Should().Be(1);
        refused.Reminder24hSentAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TheRequestIsCancelled_CarriesOn_AndCountsNothing_EvenWhenTheEmailServiceWrapsIt()
    {
        // EmailService.SendRawEmailAsync wraps EVERY exception, a cancellation included, in an
        // InvalidOperationException: the type says nothing, the token does.
        var night = PublishedNight(Guid.NewGuid());
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));
        using var cts = new CancellationTokenSource();
        _email
            .Setup(e => e.SendGameNightReminder24hEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                throw new InvalidOperationException("Failed to send raw email", new OperationCanceledException(cts.Token));
            });
        using var failures = new DispatchFailureCapture();

        var act = () => CreateHandler().Handle(Reminder(night), cts.Token);

        await act.Should().ThrowAsync<Exception>();
        failures.Count.Should().Be(0);
    }

    /// <summary>Counts the failures swallowed under THIS handler's name (other tests run in parallel).</summary>
    private sealed class DispatchFailureCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public DispatchFailureCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == "meepleai.domain_event_log.dispatch_failures.total")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "handler_name"
                        && string.Equals(tag.Value?.ToString(), nameof(GameNightReminder24hGuestEmailHandler), StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref _count);
                    }
                }
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
