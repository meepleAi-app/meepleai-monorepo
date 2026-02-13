using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for SendAgentMessageCommandHandler
/// Issue #4126: API Integration for Agent Chat
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SendAgentMessageCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepository;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<ILogger<SendAgentMessageCommandHandler>> _mockLogger;
    private readonly SendAgentMessageCommandHandler _handler;

    public SendAgentMessageCommandHandlerTests()
    {
        _mockAgentRepository = new Mock<IAgentRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<SendAgentMessageCommandHandler>>();
        _handler = new SendAgentMessageCommandHandler(
            _mockAgentRepository.Object,
            _mockLlmService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Should_Stream_Tokens_When_Agent_Exists()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        var command = new SendAgentMessageCommand(agentId, "What is Catan?");

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var chunks = new[]
        {
            new StreamChunk("Catan "),
            new StreamChunk("is "),
            new StreamChunk("a board game.")
        };

        _mockLlmService
            .Setup(s => s.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Type == StreamingEventType.StateUpdate);
        Assert.Contains(events, e => e.Type == StreamingEventType.Token);
        Assert.Contains(events, e => e.Type == StreamingEventType.Complete);

        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(3, tokenEvents.Count);
    }

    [Fact]
    public async Task Should_Return_Error_When_Agent_Not_Found()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var command = new SendAgentMessageCommand(agentId, "What is Catan?");

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.Single(events);
        var errorEvent = events[0];
        Assert.Equal(StreamingEventType.Error, errorEvent.Type);

        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Contains(agentId.ToString(), error.errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("AGENT_NOT_FOUND", error.errorCode);
    }

    [Fact]
    public async Task Should_Include_StateUpdate_Before_Tokens()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Chess Master", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        var command = new SendAgentMessageCommand(agentId, "Teach me chess");

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { new StreamChunk("Chess is...") }));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var stateUpdateEvent = events.First(e => e.Type == StreamingEventType.StateUpdate);
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(stateUpdateEvent.Data);
        Assert.Contains("Chess Master", stateUpdate.message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_Include_Complete_Event_At_End()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        var command = new SendAgentMessageCommand(agentId, "Test question");

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { new StreamChunk("Response") }));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var completeEvent = events.Last();
        Assert.Equal(StreamingEventType.Complete, completeEvent.Type);

        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.confidence > 0);
    }

    [Fact]
    public async Task Should_Skip_Empty_Token_Chunks()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        var command = new SendAgentMessageCommand(agentId, "Test");

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var chunks = new[]
        {
            new StreamChunk("Valid"),
            new StreamChunk(""),
            new StreamChunk(null),
            new StreamChunk("Token")
        };

        _mockLlmService
            .Setup(s => s.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(2, tokenEvents.Count); // Only non-empty chunks
    }

    [Fact]
    public async Task Should_Throw_When_Command_Is_Null()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _handler.Handle(null!, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
