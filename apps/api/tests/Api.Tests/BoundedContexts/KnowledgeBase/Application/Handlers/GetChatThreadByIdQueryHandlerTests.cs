using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetChatThreadByIdQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetChatThreadByIdQueryHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly GetChatThreadByIdQueryHandler _handler;

    public GetChatThreadByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _handler = new GetChatThreadByIdQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingThread_ReturnsThreadDto()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Test Thread";
        var thread = new ChatThread(threadId, userId, gameId, title);
        thread.AddUserMessage("Test message");

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var query = new GetChatThreadByIdQuery(threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(threadId);
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be(title);
        result.Status.Should().Be("active");
        result.MessageCount.Should().Be(1);
        result.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithNonExistentThread_ReturnsNull()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var query = new GetChatThreadByIdQuery(threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
