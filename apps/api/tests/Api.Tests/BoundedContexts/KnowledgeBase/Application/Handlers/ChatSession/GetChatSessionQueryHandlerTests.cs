using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("Test Session", result.Title);
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
        Assert.Null(result);
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
        Assert.NotNull(result);
        Assert.Equal(2, result.MessageCount);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("Hello", result.Messages[0].Content);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("Hi there!", result.Messages[1].Content);
        Assert.Equal("assistant", result.Messages[1].Role);
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
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(userLibraryEntryId, result.UserLibraryEntryId);
        Assert.Equal(agentSessionId, result.AgentSessionId);
        Assert.Equal("Full Session", result.Title);
        Assert.Equal(agentConfigJson, result.AgentConfigJson);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetChatSessionQueryHandler(
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetChatSessionQueryHandler(
                _mockRepository.Object,
                null!));
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
