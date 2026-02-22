using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Security-focused tests for AskQuestionQueryHandler, specifically testing
/// GameId validation to prevent cross-game data leakage via chat thread history.
///
/// Security Issue: Thread history could be loaded from a different game if ThreadId
/// is guessed or manipulated, leaking conversation context across game boundaries.
///
/// Fix: Validate thread.GameId matches query.GameId before using chat history.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AskQuestionQueryHandlerSecurityTests
{
    private readonly SearchQueryHandler _searchHandler;
    private readonly Mock<QualityTrackingDomainService> _mockQualityService;
    private readonly Mock<ChatContextDomainService> _mockChatContextService;
    private readonly Mock<IChatThreadRepository> _mockThreadRepository;
    private readonly Mock<IPdfDocumentRepository> _mockPdfDocumentRepository;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IPromptTemplateService> _mockPromptTemplateService;
    private readonly Mock<IRagValidationPipelineService> _mockValidationPipeline;
    private readonly Mock<ILogger<AskQuestionQueryHandler>> _mockLogger;
    private readonly AskQuestionQueryHandler _handler;

    public AskQuestionQueryHandlerSecurityTests()
    {
        // Create real SearchQueryHandler with mocked dependencies
        var mockEmbeddingRepo = new Mock<IEmbeddingRepository>();
        var mockVectorSearchService = new Mock<VectorSearchDomainService>();
        var mockRrfService = new Mock<RrfFusionDomainService>();
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockHybridSearchService = new Mock<IHybridSearchService>();
        var mockSearchLogger = new Mock<ILogger<SearchQueryHandler>>();

        // Setup IEmbeddingService to return valid EmbeddingResult
        mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[768] }, // 768-dimensional vector
                ErrorMessage = null
            });

        // Setup IEmbeddingRepository to return empty results by default
        mockEmbeddingRepo
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        // Setup VectorSearchDomainService
        mockVectorSearchService
            .Setup(v => v.ValidateSearchParameters(It.IsAny<int>(), It.IsAny<double>()))
            .Callback((int topK, double minScore) => { }); // No-op validation

        mockVectorSearchService
            .Setup(v => v.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>()); // Return empty list

        // Setup RRF Fusion domain service
        mockRrfService
            .Setup(r => r.FuseResults(It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<int>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>()); // Return empty list

        // Setup IHybridSearchService to return empty results by default
        mockHybridSearchService
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        _searchHandler = new SearchQueryHandler(
            mockEmbeddingRepo.Object,
            mockVectorSearchService.Object,
            mockRrfService.Object,
            mockEmbeddingService.Object,
            mockHybridSearchService.Object,
            mockSearchLogger.Object);

        _mockQualityService = new Mock<QualityTrackingDomainService>();
        _mockChatContextService = new Mock<ChatContextDomainService>();
        _mockThreadRepository = new Mock<IChatThreadRepository>();
        _mockPdfDocumentRepository = new Mock<IPdfDocumentRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockPromptTemplateService = new Mock<IPromptTemplateService>();
        _mockValidationPipeline = new Mock<IRagValidationPipelineService>();
        _mockLogger = new Mock<ILogger<AskQuestionQueryHandler>>();

        // ISSUE-977: Setup validation pipeline mock to return a valid result (all 5 layers)
        _mockValidationPipeline
            .Setup(v => v.ValidateWithMultiModelAsync(
                It.IsAny<Api.Models.QaResponse>(),
                It.IsAny<string>()!,
                It.IsAny<string>()!,
                It.IsAny<string>()!,
                It.IsAny<string>()!,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagValidationResult
            {
                IsValid = true,
                LayersPassed = 4,
                TotalLayers = 4,
                ConfidenceValidation = new ConfidenceValidationResult
                {
                    IsValid = true,
                    ValidationMessage = "Confidence is acceptable",
                    Severity = ValidationSeverity.Pass,
                    ActualConfidence = 0.8,
                    RequiredThreshold = 0.7
                },
                MultiModelConsensus = new MultiModelConsensusResult
                {
                    HasConsensus = true,
                    SimilarityScore = 0.95,
                    RequiredThreshold = 0.90,
                    Gpt4Response = new ModelResponse
                    {
                        ModelId = "gpt-4",
                        ResponseText = "Test response",
                        IsSuccess = true,
                        DurationMs = 100,
                        Usage = new LlmUsage(50, 25, 75)
                    },
                    ClaudeResponse = new ModelResponse
                    {
                        ModelId = "claude-3",
                        ResponseText = "Test response",
                        IsSuccess = true,
                        DurationMs = 100,
                        Usage = new LlmUsage(50, 25, 75)
                    },
                    ConsensusResponse = "Test response",
                    Message = "Consensus achieved",
                    TotalDurationMs = 200,
                    Severity = ConsensusSeverity.High
                },
                CitationValidation = new CitationValidationResult
                {
                    IsValid = true,
                    TotalCitations = 3,
                    ValidCitations = 3,
                    Errors = new List<CitationValidationError>(),
                    Message = "All citations valid"
                },
                HallucinationDetection = new HallucinationValidationResult
                {
                    IsValid = true,
                    DetectedKeywords = new List<string>(),
                    Language = "en",
                    TotalKeywordsChecked = 0,
                    Message = "No hallucinations detected",
                    Severity = HallucinationSeverity.None
                },
                ValidationAccuracyMetrics = "Validation accuracy tracking enabled (baseline threshold: 80%)",
                Message = "All validations passed (multi-model mode)",
                Severity = RagValidationSeverity.Pass,
                DurationMs = 250
            });

        _handler = new AskQuestionQueryHandler(
            _searchHandler,
            _mockQualityService.Object,
            _mockChatContextService.Object,
            _mockThreadRepository.Object,
            _mockPdfDocumentRepository.Object,
            _mockLlmService.Object,
            _mockPromptTemplateService.Object,
            _mockValidationPipeline.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithMatchingGameId_IncludesChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var thread = new ChatThread(threadId, userId, gameId);
        thread.AddUserMessage("Previous question");
        thread.AddAssistantMessage("Previous answer");

        SetupDefaultMocks(gameId);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(thread))
            .Returns(true);

        _mockChatContextService
            .Setup(s => s.BuildChatHistoryContext(thread))
            .Returns("Chat history context");

        _mockChatContextService
            .Setup(s => s.EnrichPromptWithHistory(It.IsAny<string>(), "Chat history context"))
            .Returns<string, string>((base_prompt, history) => $"{base_prompt}\n{history}");

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "Test question",
            ThreadId: threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockChatContextService.Verify(s => s.BuildChatHistoryContext(thread), Times.Once,
            "Chat history should be built when GameId matches");
        _mockChatContextService.Verify(s => s.EnrichPromptWithHistory(It.IsAny<string>(), "Chat history context"), Times.Once,
            "Prompt should be enriched with chat history when GameId matches");

        // Verify no warning was logged (GameId matches)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("but query is for game")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "No warning should be logged when GameId matches");
    }

    [Fact]
    public async Task Handle_WithMismatchedGameId_IgnoresChatHistory()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Thread belongs to gameId1
        var thread = new ChatThread(threadId, userId, gameId1);
        thread.AddUserMessage("Sensitive game 1 question");
        thread.AddAssistantMessage("Sensitive game 1 answer");

        SetupDefaultMocks(gameId2);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(thread))
            .Returns(true);

        // Query for gameId2 (different game)
        var query = new AskQuestionQuery(
            GameId: gameId2,
            Question: "Test question",
            ThreadId: threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);

        // CRITICAL SECURITY CHECK: Chat history should NOT be built when GameId mismatches
        _mockChatContextService.Verify(s => s.BuildChatHistoryContext(It.IsAny<ChatThread>()), Times.Never,
            "Chat history should NEVER be built when GameId does not match (prevents cross-game data leak)");

        _mockChatContextService.Verify(s => s.EnrichPromptWithHistory(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "Prompt should NEVER be enriched with chat history when GameId does not match");

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("but query is for game") && o.ToString()!.Contains("Ignoring chat history")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Warning should be logged when GameId mismatch is detected");
    }

    [Fact]
    public async Task Handle_WithNullThreadGameId_IncludesChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Thread with null GameId (generic conversation not tied to specific game)
        var thread = new ChatThread(threadId, userId, gameId: null);
        thread.AddUserMessage("General question");
        thread.AddAssistantMessage("General answer");

        SetupDefaultMocks(gameId);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(thread))
            .Returns(true);

        _mockChatContextService
            .Setup(s => s.BuildChatHistoryContext(thread))
            .Returns("Chat history context");

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "Test question",
            ThreadId: threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockChatContextService.Verify(s => s.BuildChatHistoryContext(thread), Times.Once,
            "Chat history should be included when thread.GameId is null (generic conversation)");

        // Verify no warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("but query is for game")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "No warning should be logged when thread.GameId is null");
    }

    [Fact]
    public async Task Handle_WithNonExistentThread_ContinuesWithoutHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var threadId = Guid.NewGuid();

        SetupDefaultMocks(gameId);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "Test question",
            ThreadId: threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockChatContextService.Verify(s => s.BuildChatHistoryContext(It.IsAny<ChatThread>()), Times.Never,
            "Chat history should not be built when thread does not exist");
    }

    [Fact]
    public async Task Handle_WithThreadButShouldNotIncludeHistory_DoesNotIncludeHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var thread = new ChatThread(threadId, userId, gameId);

        SetupDefaultMocks(gameId);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        // ChatContextDomainService decides not to include history (e.g., too few messages)
        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(thread))
            .Returns(false);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "Test question",
            ThreadId: threadId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockChatContextService.Verify(s => s.BuildChatHistoryContext(It.IsAny<ChatThread>()), Times.Never,
            "Chat history should not be built when ShouldIncludeChatHistory returns false");
    }

    private void SetupDefaultMocks(Guid _)
    {
        // Note: SearchQueryHandler is a real instance, not mocked
        // It will return empty results by default when its dependencies return empty data

        // Setup QualityTrackingDomainService
        _mockQualityService
            .Setup(s => s.CalculateSearchConfidence(It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>()))
            .Returns(new Confidence(0.8));

        _mockQualityService
            .Setup(s => s.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>()))
            .Returns(new Confidence(0.75));

        _mockQualityService
            .Setup(s => s.CalculateOverallConfidence(It.IsAny<Confidence>(), It.IsAny<Confidence>()))
            .Returns(new Confidence(0.77));

        _mockQualityService
            .Setup(s => s.IsLowQuality(It.IsAny<Confidence>()))
            .Returns(false);

        // Setup ChatContextDomainService (for cases where it's not explicitly mocked in tests)
        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(It.IsAny<ChatThread>()))
            .Returns(false); // Default: don't include history

        _mockChatContextService
            .Setup(s => s.EnrichPromptWithHistory(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((basePrompt, history) => $"{basePrompt}\n{history}");

        // Setup LlmService
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: "Test answer",
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost
                {
                    InputCost = 0.001m,
                    OutputCost = 0.002m,
                    ModelId = "test-model",
                    Provider = "test"
                }));

        // Setup PromptTemplateService
        _mockPromptTemplateService
            .Setup(s => s.GetActivePromptAsync("rag-system-prompt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test system prompt");
    }
}

