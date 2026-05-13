using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="NotifyAgentCreationFailedHandler"/> (Issue #940).
///
/// Verifies that <see cref="AutoAgentCreationFailedEvent"/> is translated into a
/// user-facing notification with ErrorCode-appropriate copy + deep-link, and that
/// dispatcher failures do not propagate back to the publisher's catch block.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotifyAgentCreationFailedHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<ILogger<NotifyAgentCreationFailedHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public NotifyAgentCreationFailedHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _logger = new Mock<ILogger<NotifyAgentCreationFailedHandler>>();
    }

    private NotifyAgentCreationFailedHandler CreateHandler() =>
        new(_dispatcher.Object, _logger.Object);

    private AutoAgentCreationFailedEvent CreateEvent(string errorCode, string reason = "test failure") =>
        new(
            PdfDocumentId: _pdfDocumentId,
            GameId: _gameId,
            UserId: _userId,
            ErrorCode: errorCode,
            Reason: reason);

    private NotificationMessage CaptureDispatchedMessage()
    {
        NotificationMessage? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);
        return captured!; // populated after Handle() runs; tests assert via the field
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UT-1: AGENT_SLOT_QUOTA_EXCEEDED → tier-quota copy + /settings/subscription
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_QuotaExceeded_DispatchesTierCopyAndSubscriptionDeepLink()
    {
        // Arrange
        NotificationMessage? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_SLOT_QUOTA_EXCEEDED");

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(NotificationType.AgentCreationFailed);
        captured.RecipientUserId.Should().Be(_userId);
        captured.DeepLinkPath.Should().Be("/settings/subscription");
        var payload = captured.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Be("Limite agent raggiunto");
        payload.Body.Should().Contain("limite di agent");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UT-2: TIER_FEATURE_LOCKED → feature-locked copy + /settings/subscription
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FeatureLocked_DispatchesUpgradeCopyAndSubscriptionDeepLink()
    {
        NotificationMessage? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler();
        var evt = CreateEvent("TIER_FEATURE_LOCKED");

        await handler.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeepLinkPath.Should().Be("/settings/subscription");
        var payload = captured.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Be("Funzionalità non disponibile");
        payload.Body.Should().Contain("piano superiore");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UT-3: AGENT_CREATION_FAILED (generic) → game-toolkit deep link
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GenericFailure_DispatchesGenericCopyAndToolkitDeepLink()
    {
        NotificationMessage? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_CREATION_FAILED");

        await handler.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeepLinkPath.Should().Be($"/library/private/{_gameId}/toolkit");
        var payload = captured.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Be("Creazione agent fallita");
        payload.Body.Should().Contain("Riprova manualmente");
    }

    [Fact]
    public async Task Handle_UnknownErrorCode_FallsBackToGenericCopy()
    {
        // Defensive: an ErrorCode introduced by the publisher after this handler ships
        // must NOT crash — fallback to the generic-failure branch.
        NotificationMessage? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_FUTURE_CODE_NEVER_BEFORE_SEEN");

        await handler.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        var payload = captured!.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Be("Creazione agent fallita");
    }

    [Fact]
    public async Task Handle_DispatchesExactlyOneNotification()
    {
        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_CREATION_FAILED");

        await handler.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UT-4: Dispatcher failure → handler swallows + logs (defensive)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DispatcherThrows_ExceptionIsCaughtAndDoesNotPropagate()
    {
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dispatcher unavailable"));

        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_SLOT_QUOTA_EXCEEDED");

        var act = async () => await handler.Handle(evt, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_DispatcherThrows_ErrorIsLogged()
    {
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection lost"));

        var handler = CreateHandler();
        var evt = CreateEvent("AGENT_CREATION_FAILED");

        await handler.Handle(evt, CancellationToken.None);

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("NotifyAgentCreationFailedHandler")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor null guards
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullDispatcher_ThrowsArgumentNullException()
    {
        var act = () => new NotifyAgentCreationFailedHandler(null!, _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new NotifyAgentCreationFailedHandler(_dispatcher.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
