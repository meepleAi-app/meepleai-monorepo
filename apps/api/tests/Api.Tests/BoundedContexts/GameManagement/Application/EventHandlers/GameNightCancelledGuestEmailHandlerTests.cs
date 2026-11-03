using System.Diagnostics.Metrics;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4853: a cancelled night is written to the guests who were invited BY ADDRESS. The registered users are
/// told by <c>GameNightCancelledNotificationHandler</c>; an address is not an account and nothing else reaches it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightCancelledGuestEmailHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGameNightEmailService> _email = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<ILogger<GameNightCancelledGuestEmailHandler>> _logger = new();

    private GameNightCancelledGuestEmailHandler CreateHandler() =>
        new(_invitations.Object, _nights.Object, _users.Object, _email.Object, _time, _logger.Object);

    private static GameNightInvitation Invitation(
        string email, GameNightInvitationStatus status, DateTimeOffset? expiresAt = null) =>
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
            createdBy: Guid.NewGuid());

    private static GameNightEvent Night(Guid organizerId, string title = "Friday Catan", string? zone = null) =>
        GameNightEvent.Create(
            organizerId: organizerId,
            title: title,
            scheduledAt: Now.AddDays(4),
            timeZoneId: zone);

    private static GameNightCancelledEvent Cancelled(GameNightEvent night) =>
        new(night.Id, night.OrganizerId, night.Title, new List<Guid>());

    /// <summary>The night and the invitations as the handler finds them when the event reaches it.</summary>
    private void Given(GameNightEvent night, params GameNightInvitation[] invitations)
    {
        _nights
            .Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _invitations
            .Setup(r => r.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitations);
    }

    private void VerifySentTo(string address, Times times) =>
        _email.Verify(
            e => e.SendGameNightCancelledEmailAsync(
                address,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<GameNightEmailLocale>(),
                It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public async Task Handle_WritesToEachAcceptedAndEachLivePendingInvitation_AndToNoOneElse()
    {
        // Arrange — six guests: only the first two are still expecting the night.
        var night = Night(Guid.NewGuid());
        Given(night,
            Invitation("accepted@example.com", GameNightInvitationStatus.Accepted),
            Invitation("pending@example.com", GameNightInvitationStatus.Pending),
            Invitation("declined@example.com", GameNightInvitationStatus.Declined),
            Invitation("withdrawn@example.com", GameNightInvitationStatus.Cancelled),
            Invitation("expired@example.com", GameNightInvitationStatus.Expired),
            Invitation("lapsed@example.com", GameNightInvitationStatus.Pending, expiresAt: Now.AddMinutes(-1)));

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert — one email each for the first two, none for the rest. A guest who said no, one the organizer
        // took off the night, and one whose link is dead have nothing to be told: they are not waiting for it.
        VerifySentTo("accepted@example.com", Times.Once());
        VerifySentTo("pending@example.com", Times.Once());
        VerifySentTo("declined@example.com", Times.Never());
        VerifySentTo("withdrawn@example.com", Times.Never());
        VerifySentTo("expired@example.com", Times.Never());
        VerifySentTo("lapsed@example.com", Times.Never());
    }

    [Fact]
    public async Task Handle_TheEmailCarriesTheOrganizersNameAndLanguage_AndTheNightsTitleTimeAndZone()
    {
        // Arrange
        var organizer = new Api.Tests.BoundedContexts.Authentication.TestHelpers.UserBuilder().Build();
        organizer.UpdatePreferences("it", "system", emailNotifications: true, dataRetentionDays: 90);
        _users
            .Setup(r => r.GetByIdAsync(organizer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(organizer);
        var night = Night(organizer.Id, title: "Venerdì Catan", zone: "Europe/Rome");
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert — the way the invitation was written: the organizer's language, the night's own zone.
        _email.Verify(
            e => e.SendGameNightCancelledEmailAsync(
                "guest@example.com",
                organizer.DisplayName,
                "Venerdì Catan",
                night.ScheduledAt,
                new GameNightEmailLocale("it", "Europe/Rome"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrganizerGone_StillWrites_WithTheFallbackNameOfTheInvitation()
    {
        // Arrange — the account behind the night no longer resolves: the guest is told all the same.
        var night = Night(Guid.NewGuid(), zone: "Europe/Rome");
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert — «A friend», as in the invitation this one cancels.
        _email.Verify(
            e => e.SendGameNightCancelledEmailAsync(
                "guest@example.com",
                "A friend",
                night.Title,
                night.ScheduledAt,
                new GameNightEmailLocale(null, "Europe/Rome"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TheSameAddressTwice_WritesOnce()
    {
        // Arrange — an address that accepted and was invited again: one person, one email.
        var night = Night(Guid.NewGuid());
        Given(night,
            Invitation("guest@example.com", GameNightInvitationStatus.Accepted),
            Invitation("Guest@Example.com", GameNightInvitationStatus.Pending));

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert
        _email.Invocations.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_OneSendFails_TheOthersGoOut_NothingIsThrown_AndTheFailureIsCounted()
    {
        // Arrange — the first address is refused by the transport.
        var night = Night(Guid.NewGuid());
        Given(night,
            Invitation("first@example.com", GameNightInvitationStatus.Accepted),
            Invitation("second@example.com", GameNightInvitationStatus.Accepted));
        _email
            .Setup(e => e.SendGameNightCancelledEmailAsync(
                "first@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp refused"));
        using var failures = new DispatchFailureCapture();

        // Act
        var act = () => CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert — MediatR runs the handlers of an event in sequence, so a throw would stop the ones after this
        // (and the in-app notification for the registered users). It is counted instead, under this handler's name.
        await act.Should().NotThrowAsync();
        VerifySentTo("second@example.com", Times.Once());
        failures.Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TheRequestIsCancelled_CarriesOn_AndCountsNothing()
    {
        // Arrange — a cancellation is the caller leaving, not a failure to swallow.
        var night = Night(Guid.NewGuid());
        Given(night, Invitation("guest@example.com", GameNightInvitationStatus.Accepted));
        using var cts = new CancellationTokenSource();
        _email
            .Setup(e => e.SendGameNightCancelledEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<GameNightEmailLocale>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        using var failures = new DispatchFailureCapture();

        // Act
        var act = () => CreateHandler().Handle(Cancelled(night), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        failures.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TheNightCannotBeLoaded_IsSwallowedAndCounted()
    {
        // Arrange — the read itself fails: no email goes out, and the other handlers of the event still run.
        _nights
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var night = Night(Guid.NewGuid());
        using var failures = new DispatchFailureCapture();

        // Act
        var act = () => CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _email.Invocations.Should().BeEmpty();
        failures.Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NightNotFound_WritesNothing_AndSaysSoInTheLog()
    {
        // Arrange
        var night = Night(Guid.NewGuid());
        _nights
            .Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert
        _email.Invocations.Should().BeEmpty();
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoGuestToWriteTo_DoesNotEvenLookUpTheOrganizer()
    {
        // Arrange — the common case: a night with registered players only.
        var night = Night(Guid.NewGuid());
        Given(night, Invitation("declined@example.com", GameNightInvitationStatus.Declined));

        // Act
        await CreateHandler().Handle(Cancelled(night), CancellationToken.None);

        // Assert
        _users.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _email.Invocations.Should().BeEmpty();
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
                        && string.Equals(tag.Value?.ToString(), nameof(GameNightCancelledGuestEmailHandler), StringComparison.Ordinal))
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
