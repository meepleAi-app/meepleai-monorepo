using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Comprehensive tests for StreamSetupGuideQueryHandler.
/// Tests streaming setup guide generation with progressive step delivery, LLM integration, and fallback behavior.
/// Coverage: validation, embedding, search, LLM generation, step parsing, default fallback, errors, cancellation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StreamSetupGuideQueryHandlerTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateServiceMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<StreamSetupGuideQueryHandler>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamSetupGuideQueryHandler _handler;

    public StreamSetupGuideQueryHandlerTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _qdrantServiceMock = new Mock<IQdrantService>();
        _llmServiceMock = new Mock<ILlmService>();
        _promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        _loggerMock = new Mock<ILogger<StreamSetupGuideQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Default: disable prompt database feature
        _configuration = CreateConfiguration(promptDatabaseEnabled: false);

        _handler = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _llmServiceMock.Object,
            _promptTemplateServiceMock.Object,
            _configuration,
            _loggerMock.Object,
            _fakeTimeProvider
        );
    }
    [Fact]
    public async Task Handle_ValidGameId_StreamsSetupSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupSuccessfulLlmGeneration();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);

        // State update events
        var stateUpdates = events.Where(e => e.Type == StreamingEventType.StateUpdate).ToList();
        Assert.NotEmpty(stateUpdates);
        var firstState = Assert.IsType<StreamingStateUpdate>(stateUpdates[0].Data);
        Assert.Equal("Preparing setup guide...", firstState.message);

        // Setup step events
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.NotEmpty(stepEvents);

        foreach (var stepEvent in stepEvents)
        {
            var step = Assert.IsType<StreamingSetupStep>(stepEvent.Data);
            Assert.NotNull(step.step);
            Assert.True(step.step.stepNumber > 0);
            Assert.NotEmpty(step.step.title);
            Assert.NotEmpty(step.step.instruction);
        }

        // Complete event
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.estimatedReadingTimeMinutes >= 5);
    }

    [Fact]
    public async Task Handle_LlmGeneratesSteps_ParsesCorrectly()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var llmResponse = @"STEP 1: Place the Board
Put the game board in the center of the table where all players can reach it.

STEP 2: Sort Components
Organize all cards, tokens, and pieces by type.

STEP 3: Distribute Starting Materials
Give each player 5 cards and 3 tokens as shown in the rulebook.

STEP 4: Determine First Player
The youngest player goes first.";

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock(llmResponse, totalTokens: 100);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(4, stepEvents.Count);

        var step1 = Assert.IsType<StreamingSetupStep>(stepEvents[0].Data).step;
        Assert.Equal(1, step1.stepNumber);
        Assert.Equal("Place the Board", step1.title);
        Assert.Contains("game board in the center", step1.instruction, StringComparison.OrdinalIgnoreCase);

        var step2 = Assert.IsType<StreamingSetupStep>(stepEvents[1].Data).step;
        Assert.Equal(2, step2.stepNumber);
        Assert.Equal("Sort Components", step2.title);

        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(100, complete.totalTokens);
    }

    [Fact]
    public async Task Handle_OptionalSteps_MarkedCorrectly()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var llmResponse = @"STEP 1: Required Setup
This is a required step.

STEP 2: [OPTIONAL] Advanced Variant
This step is optional for advanced players.

STEP 3: Final Setup
This is required.";

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock(llmResponse);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(3, stepEvents.Count);

        var step1 = Assert.IsType<StreamingSetupStep>(stepEvents[0].Data).step;
        Assert.False(step1.isOptional);

        var step2 = Assert.IsType<StreamingSetupStep>(stepEvents[1].Data).step;
        Assert.True(step2.isOptional);
        Assert.Equal("Advanced Variant", step2.title); // [OPTIONAL] prefix removed

        var step3 = Assert.IsType<StreamingSetupStep>(stepEvents[2].Data).step;
        Assert.False(step3.isOptional);
    }
    [Fact]
    public async Task Handle_EmbeddingFails_ReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Embedding service error"
            });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        // When embedding fails, handler returns default steps with Success=true (graceful degradation)
        // So we check for the initial "Preparing" state update instead
        var stateUpdate = events.FirstOrDefault(e => e.Type == StreamingEventType.StateUpdate);
        Assert.NotNull(stateUpdate);
        var stateData = Assert.IsType<StreamingStateUpdate>(stateUpdate.Data);
        Assert.Equal("Preparing setup guide...", stateData.message);

        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default has 5 steps

        var step1 = Assert.IsType<StreamingSetupStep>(stepEvents[0].Data).step;
        Assert.Equal("Prepare Components", step1.title);

        // Should NOT call LLM
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_QdrantReturnsNoResults_ReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = new List<SearchResultItem>() // Empty results
            });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        // Should NOT call LLM
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_LlmFails_ReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM service error"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps
    }

    [Fact]
    public async Task Handle_LlmReturnsEmptyResponse_ReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock(""); // Empty response

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps
    }

    [Fact]
    public async Task Handle_ExceptionDuringGeneration_ReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
    }
    [Fact]
    public async Task Handle_EmptyGameId_ReturnsError()
    {
        // Arrange
        var query = new StreamSetupGuideQuery("");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
        var error = Assert.IsType<StreamingError>(events[0].Data);
        Assert.Equal("Game ID is required.", error.errorMessage);
        Assert.Equal("EMPTY_GAME_ID", error.errorCode);
    }

    [Fact]
    public async Task Handle_WhitespaceGameId_ReturnsError()
    {
        // Arrange
        var query = new StreamSetupGuideQuery("   ");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
    }

    [Fact]
    public async Task Handle_NullGameId_ReturnsError()
    {
        // Arrange
        var query = new StreamSetupGuideQuery(null!);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
    }
    [Fact]
    public async Task Handle_CancellationRequested_StopsStreaming()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupSuccessfulLlmGeneration();

        var cts = new CancellationTokenSource();

        // Act
        var events = new List<RagStreamingEvent>();
        var eventCount = 0;

        try
        {
            await foreach (var evt in _handler.Handle(query, cts.Token))
            {
                events.Add(evt);
                eventCount++;

                // Cancel after initial state update
                if (eventCount == 2)
                {
                    await cts.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.InRange(events.Count, 1, 5); // Should stop early
        Assert.True(cts.IsCancellationRequested);
    }
    [Fact]
    public async Task Handle_PromptDatabaseEnabled_UsesTemplateService()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var customPrompt = "Custom setup guide system prompt from database";
        _promptTemplateServiceMock
            .Setup(x => x.GetActivePromptAsync("setup-guide-system-prompt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(customPrompt);

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Create handler with prompt database enabled
        var configWithPromptDb = CreateConfiguration(promptDatabaseEnabled: true);
        var handlerWithPromptDb = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _llmServiceMock.Object,
            _promptTemplateServiceMock.Object,
            configWithPromptDb,
            _loggerMock.Object,
            _fakeTimeProvider
        );

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handlerWithPromptDb.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        _promptTemplateServiceMock.Verify(
            x => x.GetActivePromptAsync("setup-guide-system-prompt", It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Verify LLM was called with custom prompt
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(customPrompt, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_PromptDatabaseDisabled_UsesHardcodedPrompt()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Use default handler (prompt database disabled in constructor)
        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        _promptTemplateServiceMock.Verify(
            x => x.GetActivePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        // Verify LLM was called with hardcoded fallback prompt
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.Is<string>(s => s.Contains("board game setup assistant")), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_PromptTemplateFails_FallsBackToHardcodedPrompt()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        _promptTemplateServiceMock
            .Setup(x => x.GetActivePromptAsync("setup-guide-system-prompt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Create handler with prompt database enabled
        var configWithPromptDb = CreateConfiguration(promptDatabaseEnabled: true);
        var handlerWithPromptDb = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _llmServiceMock.Object,
            _promptTemplateServiceMock.Object,
            configWithPromptDb,
            _loggerMock.Object,
            _fakeTimeProvider
        );

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handlerWithPromptDb.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert - should fallback to hardcoded prompt
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.Is<string>(s => s.Contains("board game setup assistant")), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
    [Fact]
    public async Task Handle_EstimatedTimeCalculation_MinimumFiveMinutes()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var llmResponse = "STEP 1: Quick Setup\nJust one step.";
        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock(llmResponse);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.estimatedReadingTimeMinutes >= 5); // Minimum 5 minutes
    }

    [Fact]
    public async Task Handle_MultipleSteps_CalculatesTimeCorrectly()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        // 4 steps = 4 * 2 = 8 minutes
        var llmResponse = @"STEP 1: First
Instruction 1

STEP 2: Second
Instruction 2

STEP 3: Third
Instruction 3

STEP 4: Fourth
Instruction 4";

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, CreateSampleSearchResults());
        SetupLlmMock(llmResponse);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(4, stepEvents.Count);

        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(8, complete.estimatedReadingTimeMinutes); // 4 steps * 2 min/step
    }

    [Fact]
    public async Task Handle_ConfidenceScoreFromSearchResults()
    {
        // Arrange
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Rule 1", PdfId = Guid.NewGuid().ToString(), Page = 1, Score = 0.75f },
            new SearchResultItem { Text = "Rule 2", PdfId = Guid.NewGuid().ToString(), Page = 2, Score = 0.92f },
            new SearchResultItem { Text = "Rule 3", PdfId = Guid.NewGuid().ToString(), Page = 3, Score = 0.81f }
        };

        SetupEmbeddingMock();
        SetupQdrantMock(gameId, searchResults);
        SetupLlmMock("STEP 1: Test\nInstruction");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.confidence.HasValue);
        Assert.Equal(0.92, complete.confidence.Value, 0.001); // Max score
    }
    private static IConfiguration CreateConfiguration(bool promptDatabaseEnabled)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Features:PromptDatabase"] = promptDatabaseEnabled.ToString()
        });
        return configBuilder.Build();
    }

    private void SetupSuccessfulLlmGeneration()
    {
        var llmResponse = @"STEP 1: Prepare the Game
Organize all components.

STEP 2: Setup the Board
Place the board in the center.

STEP 3: Distribute Materials
Give each player their starting items.";

        SetupEmbeddingMock();
        SetupQdrantMock("game123", CreateSampleSearchResults());
        SetupLlmMock(llmResponse);
    }

    private void SetupEmbeddingMock()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { embedding }
            });
    }

    private void SetupQdrantMock(string gameId, List<SearchResultItem> results)
    {
        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = results
            });
    }

    private void SetupLlmMock(string response, int totalTokens = 50)
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response,
                new LlmUsage(30, totalTokens - 30, totalTokens),
                null,
                null
            ));
    }

    private List<SearchResultItem> CreateSampleSearchResults()
    {
        return new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "Setup instructions for the game.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                ChunkIndex = 0,
                Score = 0.9f
            },
            new SearchResultItem
            {
                Text = "Player starting conditions.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 2,
                ChunkIndex = 1,
                Score = 0.85f
            }
        };
    }
}

