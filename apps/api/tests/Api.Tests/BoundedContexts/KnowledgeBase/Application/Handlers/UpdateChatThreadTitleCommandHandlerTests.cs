using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

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
        var thread = new ChatThread(threadId, userId);
        var newTitle = "Updated Thread Title";

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new UpdateChatThreadTitleCommand(threadId, newTitle);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(threadId);
        result.Title.Should().Be(newTitle);

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
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
