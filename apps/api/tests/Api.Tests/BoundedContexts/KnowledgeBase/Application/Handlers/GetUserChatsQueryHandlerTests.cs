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
/// Tests for GetUserChatsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetUserChatsQueryHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly GetUserChatsQueryHandler _handler;

    public GetUserChatsQueryHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _handler = new GetUserChatsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_UserHasThreads_ReturnsAllThreads()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        var thread1 = new ChatThread(Guid.NewGuid(), userId, gameId1, "Thread 1");
        thread1.AddUserMessage("Message 1");
        var thread2 = new ChatThread(Guid.NewGuid(), userId, gameId2, "Thread 2");
        thread2.AddUserMessage("Message 2");

        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread> { thread1, thread2 });

        var query = new GetUserChatsQuery(userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().Contain(t => t.Title == "Thread 1");
        result.Should().Contain(t => t.Title == "Thread 2");

        _mockRepository.Verify(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserHasNoThreads_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread>());

        var query = new GetUserChatsQuery(userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockRepository.Verify(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var threads = Enumerable.Range(1, 15)
            .Select(i =>
            {
                var thread = new ChatThread(Guid.NewGuid(), userId, gameId, $"Thread {i}");
                thread.AddUserMessage($"Message {i}");
                return thread;
            })
            .ToList();

        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetUserChatsQuery(userId, Skip: 5, Take: 5);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(5);
        result.Should().Contain(t => t.Title == "Thread 6");
        result.Should().Contain(t => t.Title == "Thread 10");
        result.Should().NotContain(t => t.Title == "Thread 1");
        result.Should().NotContain(t => t.Title == "Thread 11");

        _mockRepository.Verify(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PaginationBeyondAvailable_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var threads = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var thread = new ChatThread(Guid.NewGuid(), userId, gameId, $"Thread {i}");
                thread.AddUserMessage($"Message {i}");
                return thread;
            })
            .ToList();

        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetUserChatsQuery(userId, Skip: 10, Take: 5);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockRepository.Verify(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DefaultPagination_ReturnsFirstPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var threads = Enumerable.Range(1, 3)
            .Select(i =>
            {
                var thread = new ChatThread(Guid.NewGuid(), userId, gameId, $"Thread {i}");
                thread.AddUserMessage($"Message {i}");
                return thread;
            })
            .ToList();

        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetUserChatsQuery(userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);

        _mockRepository.Verify(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
