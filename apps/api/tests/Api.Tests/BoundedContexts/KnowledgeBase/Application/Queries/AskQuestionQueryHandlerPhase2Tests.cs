using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Translation;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Phase 2 tests for AskQuestionQueryHandler: ResponseLanguage translation,
/// IPricingEngine quota enforcement, and IHouseRuleMatcher integration.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AskQuestionQueryHandlerPhase2Tests
{
    private readonly SearchQueryHandler _searchHandler;
    private readonly Mock<QualityTrackingDomainService> _mockQualityService;
    private readonly Mock<ChatContextDomainService> _mockChatContextService;
    private readonly Mock<IChatThreadRepository> _mockThreadRepository;
    private readonly Mock<IPdfDocumentRepository> _mockPdfDocumentRepository;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IPromptTemplateService> _mockPromptTemplateService;
    private readonly Mock<IRagValidationPipelineService> _mockValidationPipeline;
    private readonly Mock<ISemanticResponseCache> _mockResponseCache;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IHouseRuleMatcher> _mockHouseRuleMatcher;
    private readonly Mock<IPricingEngine> _mockPricingEngine;
    private readonly Mock<IGenericTranslationService> _mockTranslationService;
    private readonly Mock<ILogger<AskQuestionQueryHandler>> _mockLogger;
    private readonly Mock<RrfFusionDomainService> _mockRrfService;

    public AskQuestionQueryHandlerPhase2Tests()
    {
        // Build real SearchQueryHandler with mocked dependencies
        var mockEmbeddingRepo = new Mock<IEmbeddingRepository>();
        var mockVectorSearchService = new Mock<VectorSearchDomainService>();
        _mockRrfService = new Mock<RrfFusionDomainService>();
        var mockSearchEmbeddingService = new Mock<IEmbeddingService>();
        var mockHybridSearchService = new Mock<IHybridSearchService>();
        var mockSearchLogger = new Mock<ILogger<SearchQueryHandler>>();

        mockSearchEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[768] }
            });

        mockEmbeddingRepo
            .Setup(r => r.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        mockVectorSearchService
            .Setup(v => v.ValidateSearchParameters(It.IsAny<int>(), It.IsAny<double>()))
            .Callback((int _, double _) => { });

        mockVectorSearchService
            .Setup(v => v.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>());

        _mockRrfService
            .Setup(r => r.FuseResults(It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<int>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>());

        mockHybridSearchService
            .Setup(h => h.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(), It.IsAny<int>(), It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        _searchHandler = new SearchQueryHandler(
            mockEmbeddingRepo.Object,
            mockVectorSearchService.Object,
            _mockRrfService.Object,
            mockSearchEmbeddingService.Object,
            mockHybridSearchService.Object,
            CreatePermissiveRagAccessServiceMock(),
            mockSearchLogger.Object);

        _mockQualityService = new Mock<QualityTrackingDomainService>();
        _mockChatContextService = new Mock<ChatContextDomainService>();
        _mockThreadRepository = new Mock<IChatThreadRepository>();
        _mockPdfDocumentRepository = new Mock<IPdfDocumentRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockPromptTemplateService = new Mock<IPromptTemplateService>();
        _mockValidationPipeline = new Mock<IRagValidationPipelineService>();
        _mockResponseCache = new Mock<ISemanticResponseCache>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockHouseRuleMatcher = new Mock<IHouseRuleMatcher>();
        _mockPricingEngine = new Mock<IPricingEngine>();
        _mockTranslationService = new Mock<IGenericTranslationService>();
        _mockLogger = new Mock<ILogger<AskQuestionQueryHandler>>();

        // Default: cache miss
        _mockResponseCache
            .Setup(c => c.TryGetAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedRagResponse?)null);

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[768] } });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: ResponseLanguage triggers translation when it differs from Language
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithResponseLanguageDifferentFromLanguage_TranslatesAnswer()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string originalAnswer = "The pawn moves forward one space.";
        const string translatedAnswer = "Il pedone avanza di una casella.";

        SetupDefaultMocksWithSearchResults(gameId, originalAnswer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(originalAnswer, "en", "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslationResult.CreateSuccess(translatedAnswer, "en", "it", 0.001m));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            ResponseLanguage: "it");

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Answer.Should().Be(translatedAnswer);
        _mockTranslationService.Verify(
            t => t.TranslateGenericAsync(originalAnswer, "en", "it", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: ResponseLanguage = null skips translation entirely
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithResponseLanguageNull_SkipsTranslation()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string answer = "The pawn moves forward one space.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            ResponseLanguage: null);

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Answer.Should().Be(answer);
        _mockTranslationService.Verify(
            t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: Quota denied throws ForbiddenException and no LLM call is made
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenQuotaDenied_ThrowsForbiddenExceptionWithoutLlmCall()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        SetupDefaultMocks(gameId);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), "qa_question", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en");

        var handler = BuildHandler();

        // Act
        var act = () => handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Quota*");

        _mockLlmService.Verify(
            s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "LLM must not be called when quota is denied");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: HouseRuleMatcher returns a rule — it is prepended to the user prompt
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHouseRuleMatched_PrependedToUserPrompt()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string houseRule = "In our group, the pawn can also move diagonally.";
        const string answer = "Based on your house rule, the pawn moves diagonally.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(gameId, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(houseRule);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string src, string tgt, CancellationToken _) =>
                TranslationResult.CreateSuccess(text, src, tgt, 0m));

        string? capturedUserPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((sys, user, src, ct) => capturedUserPrompt = user)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: answer,
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "test-model", Provider = "test" }));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        var handler = BuildHandler();

        // Act
        await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        capturedUserPrompt.Should().Contain("[House Rule for this group]");
        capturedUserPrompt.Should().Contain(houseRule);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: HouseRuleMatcher throws — warning logged, flow continues
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHouseRuleMatcherThrows_LogsWarningAndContinues()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string answer = "The pawn moves forward one space.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AgentMemory BC unavailable"));

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string src, string tgt, CancellationToken _) =>
                TranslationResult.CreateSuccess(text, src, tgt, 0m));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        var handler = BuildHandler();

        // Act — must not throw despite HouseRuleMatcher failure (graceful degradation)
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert: flow continued and returned a response
        result.Should().NotBeNull();

        // Warning was logged for the failure
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("House rule lookup failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "A warning should be logged when house rule lookup fails");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: Translation service fails — original answer returned + warning logged
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTranslationFails_ReturnsOriginalAnswerWithWarning()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string originalAnswer = "The pawn moves forward one space.";

        SetupDefaultMocksWithSearchResults(gameId, originalAnswer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslationResult.CreateFailure("en", "fr", "PROVIDER_ERROR", "LLM unreachable"));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            ResponseLanguage: "fr");

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert: original answer preserved
        result.Answer.Should().Be(originalAnswer);

        // Warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("Translation failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "A warning should be logged when translation fails");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7: ResponseLanguage same as Language — translation skipped (no-op)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithResponseLanguageSameAsLanguage_SkipsTranslation()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string answer = "The pawn moves forward one space.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            ResponseLanguage: "en"); // same as Language

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Answer.Should().Be(answer);
        _mockTranslationService.Verify(
            t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private AskQuestionQueryHandler BuildHandler(
        Api.Configuration.LlmQueryComplexityRoutingOptions? routingOverrides = null) =>
        new(
            _searchHandler,
            _mockQualityService.Object,
            _mockChatContextService.Object,
            _mockThreadRepository.Object,
            _mockPdfDocumentRepository.Object,
            _mockLlmService.Object,
            _mockPromptTemplateService.Object,
            _mockValidationPipeline.Object,
            CreatePermissiveRagAccessServiceMock(),
            Mock.Of<IRagQualityTracker>(),
            new QueryComplexityAnalyzer(),
            _mockResponseCache.Object,
            _mockEmbeddingService.Object,
            _mockHouseRuleMatcher.Object,
            _mockPricingEngine.Object,
            _mockTranslationService.Object,
            // D7: use the real classifier (pure, stateless, no dependencies).
            new IntentClassifierService(),
            BuildRoutingMonitor(routingOverrides ?? new Api.Configuration.LlmQueryComplexityRoutingOptions()),
            _mockLogger.Object);

    private static Microsoft.Extensions.Options.IOptionsMonitor<Api.Configuration.LlmQueryComplexityRoutingOptions> BuildRoutingMonitor(
        Api.Configuration.LlmQueryComplexityRoutingOptions value)
    {
        var monitor = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<Api.Configuration.LlmQueryComplexityRoutingOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(value);
        return monitor.Object;
    }

    private void SetupDefaultMocks(Guid gameId)
    {
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

        _mockChatContextService
            .Setup(s => s.ShouldIncludeChatHistory(It.IsAny<ChatThread>()))
            .Returns(false);
        _mockChatContextService
            .Setup(s => s.EnrichPromptWithHistory(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((b, _) => b);

        _mockPromptTemplateService
            .Setup(s => s.GetActivePromptAsync("rag-system-prompt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test system prompt");

        _mockValidationPipeline
            .Setup(v => v.ValidateWithMultiModelAsync(
                It.IsAny<Api.Models.QaResponse>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
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
                ValidationAccuracyMetrics = "Validation accuracy tracking enabled",
                Message = "All validations passed",
                Severity = RagValidationSeverity.Pass,
                DurationMs = 10
            });
    }

    private void SetupDefaultMocksWithSearchResults(Guid gameId, string llmAnswer)
    {
        SetupDefaultMocks(gameId);

        // Return one search result so the handler doesn't early-exit at "no context"
        var oneResult = new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>
        {
            new(id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "Sample rulebook text for testing.",
                pageNumber: 1,
                relevanceScore: new Confidence(0.85),
                rank: 1,
                searchMethod: "hybrid")
        };
        _mockRrfService
            .Setup(r => r.FuseResults(
                It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(),
                It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(),
                It.IsAny<int>()))
            .Returns(oneResult);

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: llmAnswer,
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost
                {
                    InputCost = 0.001m,
                    OutputCost = 0.002m,
                    ModelId = "test-model",
                    Provider = "test"
                }));
    }

    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock.Object;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #562 — per-tier LLM model dispatch tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithLowTierOverride_DispatchesGenerateCompletionWithModelAsync()
    {
        // Arrange — short factual query → Low tier (per QueryComplexityAnalyzer).
        // Config sets explicit override for Low.
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "answer");

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                "openai/gpt-4o-mini",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: "answer",
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost
                {
                    InputCost = 0.001m,
                    OutputCost = 0.002m,
                    ModelId = "openai/gpt-4o-mini",
                    Provider = "openai"
                }));

        // "Quanti giocatori?" matches LowComplexityPrefix "quanti" → Low tier.
        var query = new AskQuestionQuery(GameId: gameId, Question: "Quanti giocatori sono?");

        var handler = BuildHandler(new Api.Configuration.LlmQueryComplexityRoutingOptions
        {
            Low = "openai/gpt-4o-mini"
        });

        // Act
        await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — override dispatched, default path NOT called.
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            "openai/gpt-4o-mini",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<RequestSource>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLlmService.Verify(s => s.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<RequestSource>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoTierOverride_FallsBackToDefaultGenerateCompletionAsync()
    {
        // Arrange — empty routing config → handler uses default GenerateCompletionAsync path.
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "answer");

        _mockPricingEngine
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var query = new AskQuestionQuery(GameId: gameId, Question: "Quanti giocatori sono?");
        var handler = BuildHandler(); // default empty options

        // Act
        await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — fall back to default path, NO override dispatch.
        _mockLlmService.Verify(s => s.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<RequestSource>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<RequestSource>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void LlmQueryComplexityRoutingOptions_ResolveOverride_ReturnsNullForEmptyOrWhitespace()
    {
        // Contract: empty/whitespace tier overrides resolve to null so the handler
        // dispatches to GenerateCompletionAsync (backward-compatible no-op).
        var opts = new Api.Configuration.LlmQueryComplexityRoutingOptions
        {
            Low = "",
            Medium = "   ",
            High = "openai/gpt-4o"
        };

        opts.ResolveOverride(QueryRoutingTier.Low).Should().BeNull();
        opts.ResolveOverride(QueryRoutingTier.Medium).Should().BeNull();
        opts.ResolveOverride(QueryRoutingTier.High).Should().Be("openai/gpt-4o");
    }
}
