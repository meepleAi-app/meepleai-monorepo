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
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateServiceMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<StreamSetupGuideQueryHandler>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamSetupGuideQueryHandler _handler;

    public StreamSetupGuideQueryHandlerTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _llmServiceMock = new Mock<ILlmService>();
        _promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        _loggerMock = new Mock<ILogger<StreamSetupGuideQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Default: disable prompt database feature
        _configuration = CreateConfiguration(promptDatabaseEnabled: false);

        _handler = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
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
    public async Task Handle_LlmGeneratesSteps_QdrantRemoved_ReturnsDefaultSteps()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called. Default steps are returned instead of LLM-parsed steps.
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: Place the Board\nPut the game board in the center.", totalTokens: 100);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — Default steps returned (5 steps, not LLM-parsed)
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count);

        var step1 = Assert.IsType<StreamingSetupStep>(stepEvents[0].Data).step;
        Assert.Equal(1, step1.stepNumber);
        Assert.Equal("Prepare Components", step1.title);

        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(0, complete.totalTokens); // No LLM call, so 0 tokens
    }

    [Fact]
    public async Task Handle_OptionalSteps_QdrantRemoved_ReturnsDefaultStepsAllRequired()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called. Default steps are returned (all non-optional).
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: Required\nThis is required.\n\nSTEP 2: [OPTIONAL] Advanced\nOptional step.");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — Default steps returned (5 steps, all non-optional)
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count);

        // All default steps are non-optional
        foreach (var stepEvent in stepEvents)
        {
            var step = Assert.IsType<StreamingSetupStep>(stepEvent.Data).step;
            Assert.False(step.isOptional);
        }
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
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_NoVectorResults_ReturnsDefaultSteps()
    {
        // Arrange — Qdrant removed; handler always gets no vector results
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        // Should NOT call LLM when no vector results
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
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

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
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
        // Arrange — Qdrant removed; embedding failure triggers graceful degradation
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        using var cts = new CancellationTokenSource();

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
    public async Task Handle_PromptDatabaseEnabled_QdrantRemoved_ReturnsDefaultSteps()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called and default steps are returned regardless of prompt config.
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        var customPrompt = "Custom setup guide system prompt from database";
        _promptTemplateServiceMock
            .Setup(x => x.GetActivePromptAsync("setup-guide-system-prompt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(customPrompt);

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Create handler with prompt database enabled
        var configWithPromptDb = CreateConfiguration(promptDatabaseEnabled: true);
        var handlerWithPromptDb = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
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

        // Assert — Qdrant removed, so search always fails → default steps, LLM never called
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_PromptDatabaseDisabled_QdrantRemoved_ReturnsDefaultSteps()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called and default steps are returned.
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Use default handler (prompt database disabled in constructor)
        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — Qdrant removed, so search always fails → default steps, LLM never called
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        _promptTemplateServiceMock.Verify(
            x => x.GetActivePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_PromptTemplateFails_QdrantRemoved_ReturnsDefaultSteps()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called and default steps are returned regardless of prompt template failures.
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        _promptTemplateServiceMock
            .Setup(x => x.GetActivePromptAsync("setup-guide-system-prompt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: Test\nTest instruction");

        // Create handler with prompt database enabled
        var configWithPromptDb = CreateConfiguration(promptDatabaseEnabled: true);
        var handlerWithPromptDb = new StreamSetupGuideQueryHandler(
            _embeddingServiceMock.Object,
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

        // Assert — Qdrant removed, so search always fails → default steps, LLM never called
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count); // Default steps

        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never
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
    public async Task Handle_MultipleSteps_QdrantRemoved_DefaultStepsCalculateTimeCorrectly()
    {
        // Arrange — After Qdrant removal, SearchSetupContextAsync always returns false,
        // so the LLM is never called. Default steps (5) are returned.
        // 5 steps * 2 min/step = 10 minutes.
        var gameId = "game123";
        var query = new StreamSetupGuideQuery(gameId);

        SetupEmbeddingMock();
        SetupLlmMock("STEP 1: First\nInstruction 1");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — Default 5 steps returned
        var stepEvents = events.Where(e => e.Type == StreamingEventType.SetupStep).ToList();
        Assert.Equal(5, stepEvents.Count);

        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(10, complete.estimatedReadingTimeMinutes); // 5 steps * 2 min/step
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


    private void SetupLlmMock(string response, int totalTokens = 50)
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response,
                new LlmUsage(30, totalTokens - 30, totalTokens),
                null,
                null
            ));
    }

}

