using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for DeleteMessageCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteMessageCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<DeleteMessageCommandHandler>> _mockLogger;
    private readonly AuditService _auditService;
    private readonly DeleteMessageCommandHandler _handler;

    public DeleteMessageCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<DeleteMessageCommandHandler>>();

        // Create AuditService with null dependencies (won't be called in unit tests)
        _auditService = new AuditService(null!, Mock.Of<ILogger<AuditService>>());

        _handler = new DeleteMessageCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _auditService);
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesMessageAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Thread");
        thread.AddUserMessage("First message");
        var messageId = thread.Messages.First().Id;

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteMessageCommand(threadId, messageId, userId, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(threadId);
        result.Messages.Should().ContainSingle();
        result.Messages[0].IsDeleted.Should().BeTrue();

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

        var command = new DeleteMessageCommand(threadId, messageId, userId, false);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain(threadId.ToString());

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotOwnerAndNotAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, ownerId, gameId, "Test Thread");
        thread.AddUserMessage("First message");
        var messageId = thread.Messages.First().Id;

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteMessageCommand(threadId, messageId, differentUserId, false);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AdminDeletesOtherUserMessage_Succeeds()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, ownerId, gameId, "Test Thread");
        thread.AddUserMessage("First message");
        var messageId = thread.Messages.First().Id;

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteMessageCommand(threadId, messageId, adminId, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Messages.Should().ContainSingle();
        result.Messages[0].IsDeleted.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        var command = new DeleteMessageCommand(threadId, nonExistentMessageId, userId, false);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<KeyNotFoundException>()).Which;

        exception.Message.Should().Contain(nonExistentMessageId.ToString());
        exception.Message.Should().Contain(threadId.ToString());

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyDeletedMessage_IsIdempotent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Thread");
        thread.AddUserMessage("First message");
        var messageId = thread.Messages.First().Id;

        // First delete
        thread.DeleteMessage(messageId, userId, false);

        _mockRepository.Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new DeleteMessageCommand(threadId, messageId, userId, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Messages.Should().ContainSingle();
        result.Messages[0].IsDeleted.Should().BeTrue();

        // Should not update or save since already deleted
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
