using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

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
        Assert.NotNull(result);
        Assert.Equal(threadId, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(title, result.Title);
        Assert.Equal("active", result.Status);
        Assert.Equal(1, result.MessageCount);
        Assert.Single(result.Messages);
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
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
