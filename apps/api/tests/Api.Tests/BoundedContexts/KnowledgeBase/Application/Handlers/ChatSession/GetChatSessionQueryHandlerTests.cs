using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.ChatSession;

/// <summary>
/// Tests for GetChatSessionQueryHandler.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetChatSessionQueryHandlerTests
{
    private readonly Mock<IChatSessionRepository> _mockRepository;
    private readonly Mock<ILogger<GetChatSessionQueryHandler>> _mockLogger;
    private readonly GetChatSessionQueryHandler _handler;

    public GetChatSessionQueryHandlerTests()
    {
        _mockRepository = new Mock<IChatSessionRepository>();
        _mockLogger = new Mock<ILogger<GetChatSessionQueryHandler>>();
        _handler = new GetChatSessionQueryHandler(
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsSessionDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, userId, gameId);

        _mockRepository
            .Setup(r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetChatSessionQuery(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be("Test Session");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession?)null);

        var query = new GetChatSessionQuery(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithMessages_ReturnsMessagesInDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, Guid.NewGuid(), Guid.NewGuid());
        session.AddUserMessage("Hello");
        session.AddAssistantMessage("Hi there!");

        _mockRepository
            .Setup(r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetChatSessionQuery(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.MessageCount.Should().Be(2);
        result.Messages.Count.Should().Be(2);
        result.Messages[0].Content.Should().Be("Hello");
        result.Messages[0].Role.Should().Be("user");
        result.Messages[1].Content.Should().Be("Hi there!");
        result.Messages[1].Role.Should().Be("assistant");
    }

    [Fact]
    public async Task Handle_WithPaginationParams_PassesThemToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, Guid.NewGuid(), Guid.NewGuid());

        _mockRepository
            .Setup(r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 10, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetChatSessionQuery(
            SessionId: sessionId,
            MessageSkip: 10,
            MessageTake: 25);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 10, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllSessionFields_MapsAllFieldsToDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userLibraryEntryId = Guid.NewGuid();
        var agentSessionId = Guid.NewGuid();
        var agentConfigJson = "{\"model\":\"claude-3\"}";

        var session = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession(
            id: sessionId,
            userId: userId,
            gameId: gameId,
            title: "Full Session",
            userLibraryEntryId: userLibraryEntryId,
            agentSessionId: agentSessionId,
            agentConfigJson: agentConfigJson);
        session.Archive();

        _mockRepository
            .Setup(r => r.GetByIdWithPaginatedMessagesAsync(sessionId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetChatSessionQuery(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.UserLibraryEntryId.Should().Be(userLibraryEntryId);
        result.AgentSessionId.Should().Be(agentSessionId);
        result.Title.Should().Be("Full Session");
        result.AgentConfigJson.Should().Be(agentConfigJson);
        result.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetChatSessionQueryHandler(
                null!,
                _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetChatSessionQueryHandler(
                _mockRepository.Object,
                null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession CreateTestSession(
        Guid sessionId,
        Guid userId,
        Guid gameId)
    {
        return new Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession(
            id: sessionId,
            userId: userId,
            gameId: gameId,
            title: "Test Session");
    }
}
