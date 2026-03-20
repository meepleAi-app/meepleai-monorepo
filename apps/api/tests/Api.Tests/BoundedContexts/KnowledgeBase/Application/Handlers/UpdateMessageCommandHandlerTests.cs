using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

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

        // Create AuditService with null dependencies (won't be called in unit tests)
        _auditService = new AuditService(null!, Mock.Of<ILogger<AuditService>>());

        _handler = new UpdateMessageCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _auditService);
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesMessageAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Thread");
        thread.AddUserMessage("Original message");
        var messageId = thread.Messages.First().Id;
        var newContent = "Updated message content";

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, newContent, userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.Id.Should().Be(threadId);
        result.Messages.Should().ContainSingle();
        result.Messages[0].Content.Should().Be(newContent);
        Assert.NotNull(result.Messages[0].UpdatedAt);

        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThreadNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new UpdateMessageCommand(threadId, messageId, "New content", userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain(threadId.ToString());

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, ownerId, gameId, "Test Thread");
        thread.AddUserMessage("Original message");
        var messageId = thread.Messages.First().Id;

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, messageId, "New content", differentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MessageNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Thread");
        var nonExistentMessageId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, nonExistentMessageId, "New content", userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain(nonExistentMessageId.ToString());
        exception.Message.Should().Contain(threadId.ToString());

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateInvalidatesSubsequentAiResponses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Thread");
        thread.AddUserMessage("User message 1");
        var message1Id = thread.Messages.First().Id;
        thread.AddAssistantMessage("AI response 1");
        thread.AddUserMessage("User message 2");
        thread.AddAssistantMessage("AI response 2");

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateMessageCommand(threadId, message1Id, "Updated user message 1", userId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.Messages.Count.Should().Be(4);

        // First message should be updated
        result.Messages[0].Content.Should().Be("Updated user message 1");
        Assert.NotNull(result.Messages[0].UpdatedAt);

        // Subsequent AI response should be invalidated
        Assert.True(result.Messages[1].IsInvalidated);

        // User messages are not invalidated (only AI responses)
        Assert.False(result.Messages[2].IsInvalidated);

        // But the AI response after that user message should be invalidated
        Assert.True(result.Messages[3].IsInvalidated);

        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
