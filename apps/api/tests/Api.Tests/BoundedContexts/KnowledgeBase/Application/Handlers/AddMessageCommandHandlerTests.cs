using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for AddMessageCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddMessageCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AddMessageCommandHandler _handler;

    public AddMessageCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new AddMessageCommandHandler(_mockRepository.Object, _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithUserMessage_AddsMessageSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        var content = "What are the setup steps?";

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new AddMessageCommand(threadId, content, ChatMessage.UserRole);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal(1, result.MessageCount);
        Assert.Single(result.Messages);
        Assert.Equal(content, result.Messages[0].Content);
        Assert.Equal(ChatMessage.UserRole, result.Messages[0].Role);

        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAssistantMessage_AddsMessageSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        thread.AddUserMessage("User question");
        var content = "Here are the setup steps...";

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new AddMessageCommand(threadId, content, ChatMessage.AssistantRole);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal(2, result.MessageCount);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal(content, result.Messages[1].Content);
        Assert.Equal(ChatMessage.AssistantRole, result.Messages[1].Role);

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

        var command = new AddMessageCommand(threadId, "test content", ChatMessage.UserRole);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Handle_WithUnknownRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new AddMessageCommand(threadId, "test content", "unknown_role");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("Unknown role", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
