using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetMyChatHistoryQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetMyChatHistoryQueryHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly GetMyChatHistoryQueryHandler _handler;

    public GetMyChatHistoryQueryHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _handler = new GetMyChatHistoryQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithThreads_ReturnsPaginatedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var threads = new List<ChatThread>
        {
            CreateThread(userId, gameId, "Thread 1"),
            CreateThread(userId, gameId, "Thread 2"),
            CreateThread(userId, gameId, "Thread 3")
        };

        _mockRepository
            .Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _mockRepository
            .Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetMyChatHistoryQuery(userId, Skip: 0, Take: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Chats.Count.Should().Be(3);
        result.Chats[0].Title.Should().Be("Thread 1");
    }

    [Fact]
    public async Task Handle_WithNoThreads_ReturnsEmptyResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetMyChatHistoryQuery(userId, Skip: 0, Take: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Chats.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var threads = new List<ChatThread>
        {
            CreateThread(userId, gameId, "Thread 1"),
            CreateThread(userId, gameId, "Thread 2"),
            CreateThread(userId, gameId, "Thread 3"),
            CreateThread(userId, gameId, "Thread 4"),
            CreateThread(userId, gameId, "Thread 5")
        };

        _mockRepository
            .Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockRepository
            .Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetMyChatHistoryQuery(userId, Skip: 2, Take: 2);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(5);
        result.Chats.Count.Should().Be(2);
        result.Chats[0].Title.Should().Be("Thread 3");
        result.Chats[1].Title.Should().Be("Thread 4");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static ChatThread CreateThread(Guid userId, Guid? gameId, string title)
    {
        var thread = new ChatThread(Guid.NewGuid(), userId, gameId, title);
        thread.AddUserMessage($"Message in {title}");
        return thread;
    }
}
