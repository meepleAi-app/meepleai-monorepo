using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for CreateChatThreadCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPublisher = new Mock<IPublisher>();
        _handler = new CreateChatThreadCommandHandler(_mockRepository.Object, _mockUnitOfWork.Object, _mockPublisher.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesThreadSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(
            UserId: userId,
            GameId: gameId,
            Title: "Test Thread",
            InitialMessage: "Hello World"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("Test Thread", result.Title);
        Assert.Equal("active", result.Status);
        Assert.Equal(1, result.MessageCount); // Initial message added
        Assert.Single(result.Messages);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutInitialMessage_CreatesThreadWithoutMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(
            UserId: userId,
            GameId: null,
            Title: "Empty Thread",
            InitialMessage: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.GameId);
        Assert.Equal("Empty Thread", result.Title);
        Assert.Equal(0, result.MessageCount);
        Assert.Empty(result.Messages);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGameId_CreatesThreadSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(
            UserId: userId,
            GameId: null,
            Title: null,
            InitialMessage: "Test Message"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.GameId);
        Assert.Null(result.Title);
        Assert.Equal(1, result.MessageCount);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithWhitespaceInitialMessage_CreatesThreadWithoutMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(
            UserId: userId,
            GameId: null,
            Title: "Test",
            InitialMessage: "   " // Whitespace only
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.MessageCount);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task Handle_SetsCreatedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CreatedAt > DateTime.MinValue);
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1)); // Allow 1s tolerance
    }
}
