using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for StreamDebugQaQueryHandler.
/// Issue #461: Validates converged pipeline using RagPromptAssemblyService.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class StreamDebugQaQueryHandlerTests
{
    // ── Shared test fixtures ─────────────────────────────────────────────
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly string GameIdString = GameId.ToString();

    private readonly IRagPromptAssemblyService _ragPromptService = Substitute.For<IRagPromptAssemblyService>();
    private readonly IChatThreadRepository _chatThreadRepository = Substitute.For<IChatThreadRepository>();
    private readonly IPdfDocumentRepository _pdfDocumentRepository = Substitute.For<IPdfDocumentRepository>();
    private readonly IVectorDocumentRepository _vectorDocumentRepository = Substitute.For<IVectorDocumentRepository>();
    private readonly ISharedGameRepository _sharedGameRepository = Substitute.For<ISharedGameRepository>();
    private readonly IAgentDefinitionRepository _agentDefinitionRepository = Substitute.For<IAgentDefinitionRepository>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly IAiResponseCacheService _cache = Substitute.For<IAiResponseCacheService>();
    private readonly ILogger<StreamDebugQaQueryHandler> _logger = Substitute.For<ILogger<StreamDebugQaQueryHandler>>();
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private StreamDebugQaQueryHandler CreateSut() => new(
        _ragPromptService,
        _chatThreadRepository,
        _pdfDocumentRepository,
        _vectorDocumentRepository,
        _sharedGameRepository,
        _agentDefinitionRepository,
        _llmService,
        _cache,
        _logger,
        agentRouterService: null,
        validationPipelineService: null,
        _timeProvider);

    // ── Test 1: Resolves game context and calls AssemblePromptAsync ──────

    [Fact]
    public async Task Handle_ResolvesGameContext_AndCallsAssemblePromptAsync()
    {
        // Arrange
        var game = CreateSharedGame(GameId, "Catan", agentDefinitionId: AgentId);
        var agent = CreateAgentDefinition(AgentId, "tutor", chatLanguage: "it");

        _sharedGameRepository.GetByIdAsync(GameId, Arg.Any<CancellationToken>())
            .Returns(game);
        _agentDefinitionRepository.GetByIdAsync(AgentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        SetupCacheMiss();
        SetupEmptyDocuments();
        SetupDefaultAssembledPrompt();
        SetupDefaultLlmStream();

        var query = new StreamDebugQaQuery(GameIdString, "How do I trade?");
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert — verify AssemblePromptAsync was called with correct game context
        await _ragPromptService.Received(1).AssemblePromptAsync(
            "tutor",                                   // agentTypology from agent.Name
            "Catan",                                   // gameTitle from game.Title
            Arg.Is<GameState?>(gs => gs == null),      // gameState always null in debug
            "How do I trade?",                         // user question
            GameId,                                    // gameId
            Arg.Any<ChatThread?>(),                    // chatThread
            Arg.Is<UserTier?>(t => t != null && t.Value == "enterprise"), // admin gets Enterprise
            "it",                                      // agentLanguage from agent.ChatLanguage
            Arg.Any<CancellationToken>(),
            Arg.Any<IRagDebugEventCollector?>(),
            Arg.Any<RetrievalProfile?>());

        // Verify stream contains expected event types
        var eventTypes = events.Select(e => e.Type).ToList();
        eventTypes.Should().Contain(StreamingEventType.DebugCacheCheck);
        eventTypes.Should().Contain(StreamingEventType.DebugRetrievalStart);
        eventTypes.Should().Contain(StreamingEventType.Complete);
    }

    // ── Test 2: ConfigOverride with TopK passes profile override ─────────

    [Fact]
    public async Task Handle_WithConfigOverrideTopK_PassesProfileOverride()
    {
        // Arrange
        SetupGameWithoutAgent();
        SetupCacheMiss();
        SetupEmptyDocuments();
        SetupDefaultAssembledPrompt();
        SetupDefaultLlmStream();

        var configOverride = new DebugQaConfigOverride(TopK: 15);
        var query = new StreamDebugQaQuery(GameIdString, "What are the rules?", ConfigOverride: configOverride);
        var sut = CreateSut();

        // Act
        await CollectEventsAsync(sut, query);

        // Assert — verify AssemblePromptAsync received a profileOverride with TopK == 15
        await _ragPromptService.Received(1).AssemblePromptAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<GameState?>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<ChatThread?>(),
            Arg.Any<UserTier?>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IRagDebugEventCollector?>(),
            Arg.Is<RetrievalProfile?>(p => p != null && p.TopK == 15));
    }

    // ── Test 3: Without ConfigOverride passes null profile ────────────────

    [Fact]
    public async Task Handle_WithoutConfigOverride_PassesNullProfile()
    {
        // Arrange
        SetupGameWithoutAgent();
        SetupCacheMiss();
        SetupEmptyDocuments();
        SetupDefaultAssembledPrompt();
        SetupDefaultLlmStream();

        var query = new StreamDebugQaQuery(GameIdString, "How do I win?");
        var sut = CreateSut();

        // Act
        await CollectEventsAsync(sut, query);

        // Assert — profileOverride should be null when no ConfigOverride
        await _ragPromptService.Received(1).AssemblePromptAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<GameState?>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<ChatThread?>(),
            Arg.Any<UserTier?>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IRagDebugEventCollector?>(),
            Arg.Is<RetrievalProfile?>(p => p == null));
    }

    // ── Test 4: Game without agent uses tutor defaults ────────────────────

    [Fact]
    public async Task Handle_GameWithoutAgent_UsesTutorDefaults()
    {
        // Arrange — SharedGame with agentDefinitionId: null
        var game = CreateSharedGame(GameId, "Wingspan", agentDefinitionId: null);
        _sharedGameRepository.GetByIdAsync(GameId, Arg.Any<CancellationToken>())
            .Returns(game);

        SetupCacheMiss();
        SetupEmptyDocuments();
        SetupDefaultAssembledPrompt();
        SetupDefaultLlmStream();

        var query = new StreamDebugQaQuery(GameIdString, "How does food work?");
        var sut = CreateSut();

        // Act
        await CollectEventsAsync(sut, query);

        // Assert — falls back to "tutor" typology and "en" language
        await _ragPromptService.Received(1).AssemblePromptAsync(
            "tutor",
            "Wingspan",
            Arg.Any<GameState?>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<ChatThread?>(),
            Arg.Any<UserTier?>(),
            "en",
            Arg.Any<CancellationToken>(),
            Arg.Any<IRagDebugEventCollector?>(),
            Arg.Any<RetrievalProfile?>());
    }

    // ── Test 5: Emits all expected debug event types in correct order ─────

    [Fact]
    public async Task Handle_EmitsAllExpectedDebugEventTypes_InCorrectOrder()
    {
        // Arrange
        SetupGameWithoutAgent();
        SetupCacheMiss();
        SetupEmptyDocuments();
        SetupDefaultAssembledPrompt();
        SetupDefaultLlmStream();

        var query = new StreamDebugQaQuery(GameIdString, "Explain scoring", IncludePrompts: true);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);
        var eventTypes = events.Select(e => e.Type).ToList();

        // Assert — all expected event types are present
        eventTypes.Should().Contain(StreamingEventType.DebugCacheCheck);
        eventTypes.Should().Contain(StreamingEventType.DebugDocumentCheck);
        eventTypes.Should().Contain(StreamingEventType.DebugStrategySelected);
        eventTypes.Should().Contain(StreamingEventType.DebugRetrievalStart);
        eventTypes.Should().Contain(StreamingEventType.DebugSearchDetails);
        eventTypes.Should().Contain(StreamingEventType.DebugRetrievalResults);
        eventTypes.Should().Contain(StreamingEventType.Citations);
        eventTypes.Should().Contain(StreamingEventType.DebugPromptContext);
        eventTypes.Should().Contain(StreamingEventType.Token);
        eventTypes.Should().Contain(StreamingEventType.DebugCostUpdate);
        eventTypes.Should().Contain(StreamingEventType.Complete);

        // Assert — DebugPromptContext contains actual prompts (not hidden)
        var promptEvent = events.First(e => e.Type == StreamingEventType.DebugPromptContext);
        var promptData = promptEvent.Data as DebugPromptContextData;
        promptData.Should().NotBeNull();
        promptData!.SystemPrompt.Should().NotContain("[hidden");
        promptData.UserPrompt.Should().NotContain("[hidden");
        promptData.SystemPrompt.Should().Be("You are a board game tutor.");
        promptData.UserPrompt.Should().Be("Context: chunk1\n\nQuestion: Explain scoring");

        // Assert — ordering: CacheCheck before RetrievalStart before Complete
        var cacheIdx = eventTypes.IndexOf(StreamingEventType.DebugCacheCheck);
        var retrievalIdx = eventTypes.IndexOf(StreamingEventType.DebugRetrievalStart);
        var completeIdx = eventTypes.IndexOf(StreamingEventType.Complete);

        cacheIdx.Should().BeLessThan(retrievalIdx, "CacheCheck should come before RetrievalStart");
        retrievalIdx.Should().BeLessThan(completeIdx, "RetrievalStart should come before Complete");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SharedGame CreateSharedGame(Guid id, string title, Guid? agentDefinitionId = null)
    {
        return new SharedGame(
            id: id,
            title: title,
            yearPublished: 2020,
            description: "Test game",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "",
            thumbnailUrl: "",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow,
            modifiedAt: null,
            isDeleted: false,
            agentDefinitionId: agentDefinitionId);
    }

    private static AgentDefinition CreateAgentDefinition(Guid id, string name, string chatLanguage = "en")
    {
        var agent = new AgentDefinition(
            id: id,
            name: name,
            description: "Test agent",
            typeValue: "tutor",
            typeDescription: "Board game tutor",
            config: AgentDefinitionConfig.Default(),
            strategyJson: "{}",
            promptsJson: "[]",
            toolsJson: "[]",
            isActive: true,
            status: AgentDefinitionStatus.Published,
            createdAt: DateTime.UtcNow,
            updatedAt: null);

        if (!string.Equals(chatLanguage, "auto", StringComparison.Ordinal))
        {
            agent.SetChatLanguage(chatLanguage);
        }

        return agent;
    }

    private void SetupCacheMiss()
    {
        _cache.GenerateQaCacheKey(Arg.Any<string>(), Arg.Any<string>())
            .Returns("test-cache-key");
        _cache.GetAsync<QaResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((QaResponse?)null);
    }

    private void SetupEmptyDocuments()
    {
        _pdfDocumentRepository.FindByGameIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>());
    }

    private void SetupGameWithoutAgent()
    {
        var game = CreateSharedGame(GameId, "Test Game", agentDefinitionId: null);
        _sharedGameRepository.GetByIdAsync(GameId, Arg.Any<CancellationToken>())
            .Returns(game);
    }

    private void SetupDefaultAssembledPrompt()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc-1", 1, 0.85f, "Some rule text about scoring")
        };

        var assembled = new AssembledPrompt(
            "You are a board game tutor.",
            "Context: chunk1\n\nQuestion: Explain scoring",
            citations,
            500);

        _ragPromptService.AssemblePromptAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<GameState?>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<ChatThread?>(),
            Arg.Any<UserTier?>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IRagDebugEventCollector?>(),
            Arg.Any<RetrievalProfile?>())
            .Returns(assembled);
    }

    private void SetupDefaultLlmStream()
    {
        var usage = new LlmUsage(100, 50, 150);
        var cost = new LlmCost
        {
            InputCost = 0.001m,
            OutputCost = 0.002m,
            ModelId = "gpt-4",
            Provider = "openai"
        };

        _llmService.GenerateCompletionStreamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<RequestSource>(),
            Arg.Any<CancellationToken>())
            .Returns(CreateStreamChunks("The answer is 42", usage, cost));
    }

    private static async IAsyncEnumerable<StreamChunk> CreateStreamChunks(
        string text,
        LlmUsage usage,
        LlmCost cost)
    {
        var words = text.Split(' ');
        foreach (var word in words)
        {
            yield return new StreamChunk(word + " ");
            await Task.Yield();
        }

        // Final chunk with usage metadata
        yield return new StreamChunk(null, usage, cost, IsFinal: true);
    }

    private static async Task<List<RagStreamingEvent>> CollectEventsAsync(
        StreamDebugQaQueryHandler handler,
        StreamDebugQaQuery query)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handler.Handle(query, CancellationToken.None))
        {
            events.Add(evt);
        }
        return events;
    }
}
