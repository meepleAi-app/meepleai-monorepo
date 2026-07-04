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
/// Tests for GetChatThreadsByGameQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetChatThreadsByGameQueryHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly GetChatThreadsByGameQueryHandler _handler;

    public GetChatThreadsByGameQueryHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _handler = new GetChatThreadsByGameQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_UserAndGameHaveThreads_ReturnsThreads()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var thread1 = new ChatThread(Guid.NewGuid(), userId, gameId, "Thread 1");
        thread1.AddUserMessage("Message 1");
        var thread2 = new ChatThread(Guid.NewGuid(), userId, gameId, "Thread 2");
        thread2.AddUserMessage("Message 2");

        _mockRepository.Setup(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread> { thread1, thread2 });

        var query = new GetChatThreadsByGameQuery(gameId, userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().AllSatisfy(t => t.GameId.Should().Be(gameId));
        result.Should().Contain(t => t.Title == "Thread 1");
        result.Should().Contain(t => t.Title == "Thread 2");

        _mockRepository.Verify(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoThreadsForGameAndUser_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockRepository.Setup(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread>());

        var query = new GetChatThreadsByGameQuery(gameId, userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockRepository.Verify(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var threads = Enumerable.Range(1, 10)
            .Select(i =>
            {
                var thread = new ChatThread(Guid.NewGuid(), userId, gameId, $"Thread {i}");
                thread.AddUserMessage($"Message {i}");
                return thread;
            })
            .ToList();

        _mockRepository.Setup(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var query = new GetChatThreadsByGameQuery(gameId, userId, Skip: 3, Take: 4);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(4);
        result.Should().Contain(t => t.Title == "Thread 4");
        result.Should().Contain(t => t.Title == "Thread 7");
        result.Should().NotContain(t => t.Title == "Thread 1");
        result.Should().NotContain(t => t.Title == "Thread 8");

        _mockRepository.Verify(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test for Issue #2500: CitationsJson and agent metadata must survive
    /// the entity → DTO mapping so the FE can display citations after a reload.
    /// </summary>
    [Fact]
    public async Task Handle_AssistantMessageWithMetadata_MapsCitationsJsonAndAgentMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var citationsJson = "[{\"source\":\"Reg\",\"pageNumber\":7}]";
        const string agentType = "arbitro";
        const float confidence = 0.85f;
        const int tokenCount = 256;

        var thread = new ChatThread(Guid.NewGuid(), userId, gameId, "Citations Thread");
        thread.AddUserMessage("Who wins in case of a tie?");
        thread.AddAssistantMessageWithMetadata(
            content: "In case of a tie, the player with the most roads wins.",
            agentType: agentType,
            confidence: confidence,
            citationsJson: citationsJson,
            tokenCount: tokenCount);

        _mockRepository.Setup(r => r.FindByUserIdAndGameIdAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread> { thread });

        var query = new GetChatThreadsByGameQuery(gameId, userId, 0, 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var returnedThread = result.Single();
        var assistantMsg = returnedThread.Messages.Single(m => m.Role == "assistant");
        assistantMsg.CitationsJson.Should().Be(citationsJson);
        assistantMsg.AgentType.Should().Be(agentType);
        assistantMsg.Confidence.Should().BeApproximately(confidence, 0.001f);
        assistantMsg.TokenCount.Should().Be(tokenCount);
    }
}
