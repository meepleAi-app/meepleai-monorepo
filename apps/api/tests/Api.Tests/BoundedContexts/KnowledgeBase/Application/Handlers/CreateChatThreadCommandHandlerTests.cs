using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CreateChatThreadCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IAgentRepository> _mockAgentRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockAgentRepository = new Mock<IAgentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPublisher = new Mock<IPublisher>();
        _handler = new CreateChatThreadCommandHandler(_mockRepository.Object, _mockAgentRepository.Object, _mockUnitOfWork.Object, _mockPublisher.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateChatThreadCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithInitialMessage_CreatesThreadWithMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Test Thread";
        var initialMessage = "Hello, how do I play this game?";
        var command = new CreateChatThreadCommand(userId, gameId, title, initialMessage);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(title, result.Title);
        Assert.Equal("active", result.Status);
        Assert.Equal(1, result.MessageCount);
        Assert.Single(result.Messages);
        Assert.Equal(initialMessage, result.Messages[0].Content);
        Assert.Equal("user", result.Messages[0].Role);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutInitialMessage_CreatesEmptyThread()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Empty Thread";
        var command = new CreateChatThreadCommand(userId, gameId, title, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(title, result.Title);
        Assert.Equal("active", result.Status);
        Assert.Equal(0, result.MessageCount);
        Assert.Empty(result.Messages);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGameId_CreatesThreadWithNullGameId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var title = "General Thread";
        var command = new CreateChatThreadCommand(userId, null, title, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.GameId);
        Assert.Equal(title, result.Title);
        Assert.Equal("active", result.Status);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
