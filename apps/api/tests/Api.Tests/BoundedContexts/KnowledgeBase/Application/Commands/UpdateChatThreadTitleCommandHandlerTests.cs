using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for UpdateChatThreadTitleCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateChatThreadTitleCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateChatThreadTitleCommandHandler _handler;

    public UpdateChatThreadTitleCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UpdateChatThreadTitleCommandHandler(_mockRepository.Object, _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidTitle_UpdatesSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, title: "Old Title");
        thread.AddUserMessage("Test message");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, "New Title");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal("New Title", result.Title);
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentThread_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new UpdateChatThreadTitleCommand(threadId, "New Title");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", exception.Message);
        Assert.Contains(threadId.ToString(), exception.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceTitle_TrimsWhitespace()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, "  Spaced Title  ");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Spaced Title", result.Title); // Domain should trim
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatesThreadWithMessages()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, title: "Original");
        thread.AddUserMessage("Message 1");
        thread.AddAssistantMessage("Response 1");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, "Updated Title");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(2, result.MessageCount); // Messages preserved
        Assert.Equal(2, result.Messages.Count);
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatesClosedThread()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, title: "Old Title");
        thread.CloseThread();

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, "New Title for Closed");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Title for Closed", result.Title);
        Assert.Equal("closed", result.Status);
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PreservesAllThreadProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Original Title");
        thread.AddUserMessage("Message");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, "New Title");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("New Title", result.Title);
        Assert.Equal(1, result.MessageCount);
    }
}
