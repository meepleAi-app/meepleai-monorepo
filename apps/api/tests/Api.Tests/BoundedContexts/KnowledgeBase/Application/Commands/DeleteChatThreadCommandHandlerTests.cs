using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for DeleteChatThreadCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<DeleteChatThreadCommandHandler>> _mockLogger;
    private readonly DeleteChatThreadCommandHandler _handler;

    public DeleteChatThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<DeleteChatThreadCommandHandler>>();
        _handler = new DeleteChatThreadCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidOwnership_DeletesThreadSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("Test message");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteChatThreadCommand(threadId, userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.DeleteAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentThread_ReturnsFalse()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new DeleteChatThreadCommand(threadId, userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidOwnership_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid(); // Different user
        var thread = new ChatThread(threadId, ownerId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteChatThreadCommand(threadId, requestingUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("does not have permission", exception.Message);
        Assert.Contains(threadId.ToString(), exception.Message);
        Assert.Contains(requestingUserId.ToString(), exception.Message);
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeletesThreadWithMultipleMessages()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("Message 1");
        thread.AddAssistantMessage("Response 1");
        thread.AddUserMessage("Message 2");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteChatThreadCommand(threadId, userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Equal(3, thread.MessageCount);
        _mockRepository.Verify(r => r.DeleteAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeletesClosedThread()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.CloseThread();

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteChatThreadCommand(threadId, userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.DeleteAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }
}
