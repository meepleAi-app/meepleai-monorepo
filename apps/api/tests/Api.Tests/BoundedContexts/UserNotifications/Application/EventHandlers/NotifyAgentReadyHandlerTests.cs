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
/// Unit tests for NotifyAgentReadyHandler.
/// Verifies that when AgentAutoCreatedEvent is raised, the user receives
/// an in-app notification directing them to the game's toolkit page.
///
/// Test scenarios:
/// - Happy path: dispatches notification with correct Italian message, type, and deep link
/// - Dispatcher failure: exception is caught and does not propagate
/// - Constructor null guards: each dependency throws ArgumentNullException
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotifyAgentReadyHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<ILogger<NotifyAgentReadyHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _privateGameId = Guid.NewGuid();
    private const string GameName = "Twilight Imperium";

    public NotifyAgentReadyHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _logger = new Mock<ILogger<NotifyAgentReadyHandler>>();
    }

    private NotifyAgentReadyHandler CreateHandler() =>
        new(_dispatcher.Object, _logger.Object);

    private AgentAutoCreatedEvent CreateEvent(
        string? gameName = null,
        Guid? userId = null,
        Guid? agentId = null,
        Guid? privateGameId = null) =>
        new(
            agentDefinitionId: agentId ?? _agentId,
            privateGameId: privateGameId ?? _privateGameId,
            userId: userId ?? _userId,
            gameName: gameName ?? GameName);

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path: notification dispatched with correct properties
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DispatchesNotificationWithCorrectType()
    {
        // Arrange
        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.AgentAutoCreated),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchesNotificationToCorrectUser()
    {
        // Arrange
        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchesNotificationWithCorrectDeepLink()
    {
        // Arrange
        var handler = CreateHandler();
        var evt = CreateEvent();
        var expectedLink = $"/library/private/{_privateGameId}/toolkit";

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.DeepLinkPath == expectedLink),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchesNotificationWithItalianGameNameInPayload()
    {
        // Arrange
        var handler = CreateHandler();
        var evt = CreateEvent(gameName: GameName);

        NotificationMessage? capturedMessage = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — Italian message contains the game name
        capturedMessage.Should().NotBeNull();
        var payload = capturedMessage!.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Contain(GameName);
        payload.Body.Should().Contain(GameName);
        payload.Body.Should().Contain("creato automaticamente");
    }

    [Fact]
    public async Task Handle_DispatchesExactlyOneNotification()
    {
        // Arrange
        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Resilience: dispatcher failure must not propagate
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DispatcherThrows_ExceptionIsCaughtAndDoesNotPropagate()
    {
        // Arrange
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dispatcher unavailable"));

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act — must not throw; notification failure is non-critical
        var act = async () => await handler.Handle(evt, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_DispatcherThrows_ErrorIsLogged()
    {
        // Arrange
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection lost"));

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — error was logged with enough context
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("NotifyAgentReadyHandler")),
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
        var act = () =>
            new NotifyAgentReadyHandler(null!, _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act2 = () =>
            new NotifyAgentReadyHandler(_dispatcher.Object, null!);
        act2.Should().Throw<ArgumentNullException>();
    }
}
