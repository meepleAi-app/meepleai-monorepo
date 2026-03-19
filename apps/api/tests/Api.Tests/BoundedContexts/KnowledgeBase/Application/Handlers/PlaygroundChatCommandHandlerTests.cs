using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for PlaygroundChatCommandHandler
/// Issue #4392: Backend Integration - SSE Endpoint with real AgentDefinition
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class PlaygroundChatCommandHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockAgentDefinitionRepository;
    private readonly Mock<ILlmClient> _mockLlmClient;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly Mock<ILlmCostCalculator> _mockCostCalculator;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;
    private readonly Mock<IRagExecutionRepository> _mockRagExecutionRepository;
    private readonly Mock<ILogger<PlaygroundChatCommandHandler>> _mockLogger;
    private readonly PlaygroundChatCommandHandler _handler;

    public PlaygroundChatCommandHandlerTests()
    {
        _mockAgentDefinitionRepository = new Mock<IAgentDefinitionRepository>();
        _mockLlmClient = new Mock<ILlmClient>();
        _mockLlmClient.Setup(c => c.ProviderName).Returns("TestProvider");
        _mockLlmClient.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        _llmProviderFactory = new LlmProviderFactory(
            new[] { _mockLlmClient.Object },
            new Mock<ILogger<LlmProviderFactory>>().Object);

        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _mockCostCalculator = new Mock<ILlmCostCalculator>();
        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
        _mockRagExecutionRepository = new Mock<IRagExecutionRepository>();
        _mockLogger = new Mock<ILogger<PlaygroundChatCommandHandler>>();

        // Set up cost calculator to return valid result (avoids NRE in cost logging)
        _mockCostCalculator
            .Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(LlmCostCalculation.Empty);

        _handler = new PlaygroundChatCommandHandler(
            _mockAgentDefinitionRepository.Object,
            _llmProviderFactory,
            _mockHybridSearchService.Object,
            _mockCostCalculator.Object,
            _mockCostLogRepository.Object,
            _mockRagExecutionRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Should_Stream_Tokens_When_AgentDefinition_Exists()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Game Tutor");
        var command = new PlaygroundChatCommand(agentDefId, "What is Catan?");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        var chunks = new[]
        {
            new StreamChunk("Catan "),
            new StreamChunk("is "),
            new StreamChunk("a popular board game where players collect resources and build settlements.")
        };

        SetupLlmClientStream(chunks);

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
    public async Task Should_Return_Error_When_AgentDefinition_Not_Found()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var command = new PlaygroundChatCommand(agentDefId, "Test question");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinitionEntity?)null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - StateUpdate("Loading...") + Error
        Assert.Equal(2, events.Count);
        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);

        var errorEvent = events[1];
        Assert.Equal(StreamingEventType.Error, errorEvent.Type);

        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Contains(agentDefId.ToString(), error.errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("AGENT_NOT_FOUND", error.errorCode);
    }

    [Fact]
    public async Task Should_Return_Error_When_AgentDefinition_Is_Inactive()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateInactiveAgentDefinition(agentDefId, "Disabled Agent");
        var command = new PlaygroundChatCommand(agentDefId, "Test question");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - StateUpdate("Loading...") + Error
        Assert.Equal(2, events.Count);
        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);
        var error = Assert.IsType<StreamingError>(events[1].Data);
        Assert.Equal("AGENT_INACTIVE", error.errorCode);
    }

    [Fact]
    public async Task Should_Include_StateUpdate_With_Agent_Name()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Chess Master AI");
        var command = new PlaygroundChatCommand(agentDefId, "Teach me chess");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Chess is a strategy game.") });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var stateEvents = events.Where(e => e.Type == StreamingEventType.StateUpdate).ToList();
        Assert.True(stateEvents.Count >= 1);

        // StateUpdate should mention the agent name or model
        var agentLoadedEvent = stateEvents.Last();
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(agentLoadedEvent.Data);
        Assert.True(
            stateUpdate.message.Contains("Chess Master AI", StringComparison.OrdinalIgnoreCase)
            || stateUpdate.message.Contains("gpt-4", StringComparison.OrdinalIgnoreCase),
            $"Expected state update to mention agent name or model, got: {stateUpdate.message}");
    }

    [Fact]
    public async Task Should_Include_PlaygroundComplete_With_Metadata()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Test Agent");
        var command = new PlaygroundChatCommand(agentDefId, "Test question");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Response text here") });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var completeEvent = events.Last();
        Assert.Equal(StreamingEventType.Complete, completeEvent.Type);

        var complete = Assert.IsType<PlaygroundStreamingComplete>(completeEvent.Data);
        Assert.NotNull(complete.agentConfig);
        Assert.Equal("Test Agent", complete.agentConfig.AgentName);
        Assert.Equal("gpt-4", complete.agentConfig.Model);
        Assert.Equal("TestProvider", complete.agentConfig.Provider);
        Assert.NotNull(complete.latencyBreakdown);
        Assert.True(complete.latencyBreakdown.totalMs >= 0);
    }

    [Fact]
    public async Task Should_Include_FollowUpQuestions_For_Long_Responses()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Verbose Agent");
        var command = new PlaygroundChatCommand(agentDefId, "Explain board games");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        // Generate a response longer than 50 chars to trigger follow-up questions
        var longResponse = new string('A', 60);
        SetupLlmClientStream(new[] { new StreamChunk(longResponse) });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.Contains(events, e => e.Type == StreamingEventType.FollowUpQuestions);
        var fuqEvent = events.First(e => e.Type == StreamingEventType.FollowUpQuestions);
        var fuq = Assert.IsType<StreamingFollowUpQuestions>(fuqEvent.Data);
        Assert.NotEmpty(fuq.questions);
    }

    [Fact]
    public async Task Should_Skip_Empty_Token_Chunks()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "TestAgent");
        var command = new PlaygroundChatCommand(agentDefId, "Test");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        var chunks = new[]
        {
            new StreamChunk("Valid"),
            new StreamChunk(""),
            new StreamChunk(null),
            new StreamChunk("Token")
        };

        SetupLlmClientStream(chunks);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(2, tokenEvents.Count);
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

    [Fact]
    public async Task Should_Use_System_Prompt_From_AgentDefinition()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Custom Agent",
            systemPrompt: "You are a chess expert. Only discuss chess.");
        var command = new PlaygroundChatCommand(agentDefId, "Tell me about chess");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Chess is a two-player strategy game") });

        // Act
        await foreach (var _ in _handler.Handle(command, CancellationToken.None))
        {
            // Consume events
        }

        // Assert - verify the system prompt from AgentDefinition was passed to LLM client
        _mockLlmClient.Verify(c => c.GenerateCompletionStreamAsync(
            "gpt-4",
            "You are a chess expert. Only discuss chess.",
            "Tell me about chess",
            It.Is<double>(d => Math.Abs(d - 0.7) < 0.01),
            2048,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Use_Fallback_System_Prompt_When_No_Template()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Bare Agent", systemPrompt: null);
        var command = new PlaygroundChatCommand(agentDefId, "Hello");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Hi!") });

        // Act
        await foreach (var _ in _handler.Handle(command, CancellationToken.None))
        {
            // Consume events
        }

        // Assert - fallback prompt should contain the agent name
        _mockLlmClient.Verify(c => c.GenerateCompletionStreamAsync(
            "gpt-4",
            It.Is<string>(p => p.Contains("Bare Agent", StringComparison.OrdinalIgnoreCase)),
            "Hello",
            It.Is<double>(d => Math.Abs(d - 0.7) < 0.01),
            2048,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Track_Token_Usage_From_Final_Chunk()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Token Tracker");
        var command = new PlaygroundChatCommand(agentDefId, "Count tokens");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        var chunks = new[]
        {
            new StreamChunk("Word1 "),
            new StreamChunk("Word2 "),
            new StreamChunk(null, new LlmUsage(100, 50, 150), null, true)
        };

        SetupLlmClientStream(chunks);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var completeEvent = events.Last();
        var complete = Assert.IsType<PlaygroundStreamingComplete>(completeEvent.Data);
        Assert.Equal(100, complete.promptTokens);
        Assert.Equal(50, complete.completionTokens);
        Assert.Equal(150, complete.totalTokens);
    }

    [Fact]
    public async Task Should_Not_Persist_To_ChatThread()
    {
        // Playground is ephemeral - no persistence
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Ephemeral Agent");
        var command = new PlaygroundChatCommand(agentDefId, "Test");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Response") });

        // Act
        await foreach (var _ in _handler.Handle(command, CancellationToken.None))
        {
        }

        // Assert - handler has no ChatThreadRepository dependency, so nothing persisted
        // Validated by verifying the LLM client was called once
        _mockLlmClient.Verify(
            c => c.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Emit_Citations_When_GameId_Provided()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "RAG Agent");
        var command = new PlaygroundChatCommand(agentDefId, "How do I set up the game?", gameId);

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        _mockHybridSearchService
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId, SearchMode.Hybrid, 5,
                null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                new()
                {
                    ChunkId = "chunk-1",
                    Content = "Place the board in the center of the table.",
                    PdfDocumentId = Guid.NewGuid().ToString(),
                    GameId = gameId,
                    ChunkIndex = 0,
                    PageNumber = 3,
                    HybridScore = 0.85f,
                    Mode = SearchMode.Hybrid
                }
            });

        SetupLlmClientStream(new[] { new StreamChunk("Set up by placing the board in the center.") });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.Contains(events, e => e.Type == StreamingEventType.Citations);
        var citationsEvent = events.First(e => e.Type == StreamingEventType.Citations);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);
        Assert.Single(citations.citations);
        Assert.Contains("Place the board", citations.citations[0].text, StringComparison.OrdinalIgnoreCase);

        // Verify confidence is set from RAG results
        var completeEvent = events.Last();
        var complete = Assert.IsType<PlaygroundStreamingComplete>(completeEvent.Data);
        Assert.NotNull(complete.confidence);
        Assert.True(complete.confidence > 0);
    }

    [Fact]
    public async Task Should_Not_Search_When_No_GameId()
    {
        // Arrange
        var agentDefId = Guid.NewGuid();
        var agentDef = CreateActiveAgentDefinition(agentDefId, "Pure LLM Agent");
        var command = new PlaygroundChatCommand(agentDefId, "What is chess?");

        _mockAgentDefinitionRepository
            .Setup(r => r.GetByIdAsync(agentDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        SetupLlmClientStream(new[] { new StreamChunk("Chess is a strategy game.") });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - no citations, no search called
        Assert.DoesNotContain(events, e => e.Type == StreamingEventType.Citations);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(),
                It.IsAny<int>(), It.IsAny<List<Guid>?>(), It.IsAny<float>(),
                It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #region Helpers

    private void SetupLlmClientStream(StreamChunk[] chunks)
    {
        _mockLlmClient
            .Setup(c => c.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));
    }

    private static AgentDefinitionEntity CreateActiveAgentDefinition(
        Guid id, string name, string? systemPrompt = null)
    {
        var prompts = new List<AgentPromptTemplate>();
        if (systemPrompt != null)
        {
            prompts.Add(AgentPromptTemplate.Create("system", systemPrompt));
        }

        return AgentDefinitionEntity.Create(
            name: name,
            description: $"Test agent: {name}",
            type: AgentType.Custom("rag", "RAG-based assistant"),
            config: AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f),
            prompts: prompts);
    }

    private static AgentDefinitionEntity CreateInactiveAgentDefinition(Guid id, string name)
    {
        var agentDef = AgentDefinitionEntity.Create(
            name: name,
            description: $"Inactive test agent: {name}",
            type: AgentType.Custom("rag", "RAG-based assistant"),
            config: AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f));

        agentDef.Deactivate();
        return agentDef;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    #endregion
}
