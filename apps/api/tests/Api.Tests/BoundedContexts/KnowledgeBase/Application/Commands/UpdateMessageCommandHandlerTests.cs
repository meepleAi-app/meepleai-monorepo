using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for UpdateMessageCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateMessageCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<UpdateMessageCommandHandler>> _mockLogger;
    private readonly AuditService _auditService;
    private readonly UpdateMessageCommandHandler _handler;

    public UpdateMessageCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<UpdateMessageCommandHandler>>();
        _auditService = new AuditService(null!, Mock.Of<ILogger<AuditService>>());
        _handler = new UpdateMessageCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _auditService
        );
    }

    [Fact]
    public async Task Handle_WithValidOwnership_UpdatesMessageSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("Original content");
        var messageId = thread.Messages.First().Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, "Updated content", userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal("Updated content", thread.Messages.First().Content);
        Assert.NotNull(thread.Messages.First().UpdatedAt);
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentThread_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new UpdateMessageCommand(threadId, messageId, "New content", userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", exception.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var thread = new ChatThread(threadId, ownerId);
        thread.AddUserMessage("Message");
        var messageId = thread.Messages.First().Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, "Updated content", otherUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("your own threads", exception.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentMessage_ThrowsKeyNotFoundException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        var nonExistentMessageId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, nonExistentMessageId, "New content", userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found in thread", exception.Message);
        Assert.Contains(nonExistentMessageId.ToString(), exception.Message);
    }

    [Fact]
    public async Task Handle_InvalidatesSubsequentAIResponses()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("User message");
        thread.AddAssistantMessage("AI response 1");
        thread.AddUserMessage("Another user message");
        thread.AddAssistantMessage("AI response 2");

        var firstMessageId = thread.Messages.First(m => m.Role == "user").Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, firstMessageId, "Updated user message", userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var updatedMessage = thread.Messages.First(m => m.Id == firstMessageId);
        Assert.Equal("Updated user message", updatedMessage.Content);
        Assert.NotNull(updatedMessage.UpdatedAt);
        // Subsequent AI responses should be invalidated
        var assistantMessages = thread.Messages.Where(m => m.Role == "assistant").ToList();
        Assert.True(assistantMessages.All(m => m.IsInvalidated));
    }

    [Fact]
    public async Task Handle_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("Original");
        var messageId = thread.Messages.First().Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, "Updated", userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var message = thread.Messages.First();
        Assert.NotNull(message.UpdatedAt); // UpdatedAt is set by UpdateContent
        Assert.True(message.UpdatedAt >= thread.CreatedAt); // Should be after or equal to creation
    }

    [Fact]
    public async Task Handle_WithDeletedMessage_CannotUpdate()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("Message");
        var messageId = thread.Messages.First().Id;
        thread.DeleteMessage(messageId, userId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, "New content", userId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
