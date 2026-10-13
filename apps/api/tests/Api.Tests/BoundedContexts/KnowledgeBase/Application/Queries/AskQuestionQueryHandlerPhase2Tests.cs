using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Translation;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
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
/// the daily quota of the plan (#4168, 5g-b), and IHouseRuleMatcher integration.
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
    private readonly Mock<IRagValidationPipelineService> _mockValidationPipeline;
    private readonly Mock<ISemanticResponseCache> _mockResponseCache;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IHouseRuleMatcher> _mockHouseRuleMatcher;
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
        var mockKeywordSearchService = new Mock<IKeywordSearchService>();
        var mockSearchLogger = new Mock<ILogger<SearchQueryHandler>>();

        mockSearchEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[768] }
            });

        mockEmbeddingRepo
            .Setup(r => r.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync.
        mockEmbeddingRepo
            .Setup(r => r.SearchByVectorWithScoresAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());

        mockVectorSearchService
            .Setup(v => v.ValidateSearchParameters(It.IsAny<int>(), It.IsAny<double>()))
            .Callback((int _, double _) => { });

        mockVectorSearchService
            .Setup(v => v.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>());

        _mockRrfService
            .Setup(r => r.FuseResults(It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>());

        mockKeywordSearchService
            .Setup(h => h.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        _searchHandler = new SearchQueryHandler(
            mockEmbeddingRepo.Object,
            mockVectorSearchService.Object,
            _mockRrfService.Object,
            mockSearchEmbeddingService.Object,
            mockKeywordSearchService.Object,
            CreatePermissiveRagAccessServiceMock(),
            mockSearchLogger.Object);

        _mockQualityService = new Mock<QualityTrackingDomainService>();
        _mockChatContextService = new Mock<ChatContextDomainService>();
        _mockThreadRepository = new Mock<IChatThreadRepository>();
        _mockPdfDocumentRepository = new Mock<IPdfDocumentRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockValidationPipeline = new Mock<IRagValidationPipelineService>();
        _mockResponseCache = new Mock<ISemanticResponseCache>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockHouseRuleMatcher = new Mock<IHouseRuleMatcher>();
        _mockTranslationService = new Mock<IGenericTranslationService>();
        _mockLogger = new Mock<ILogger<AskQuestionQueryHandler>>();

        // Default: cache miss
        _mockResponseCache
            .Setup(c => c.TryGetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedRagResponse?)null);

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
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
    // Test 3 (#4168, 5g-b): the daily quota of the plan. It replaces IPricingEngine, whose only
    // implementation let every question through.
    // ─────────────────────────────────────────────────────────────────────────

    private static AskQuestionQuery AQuestionFrom(Guid userId, Guid gameId) => new(
        GameId: gameId,
        Question: "How does the pawn move?",
        Language: "en",
        UserId: userId,
        UserRole: "User");

    private static void NothingWasCounted(Mock<Api.SharedKernel.Services.ITierEnforcementService> tiers) =>
        tiers.Verify(
            t => t.RecordUsageAsync(
                It.IsAny<Guid>(), It.IsAny<Api.SharedKernel.Services.TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static void OneAnswerWasCountedFor(Guid userId, Mock<Api.SharedKernel.Services.ITierEnforcementService> tiers) =>
        tiers.Verify(
            t => t.RecordUsageAsync(
                userId, Api.SharedKernel.Services.TierAction.AgentQuery, It.IsAny<CancellationToken>()),
            Times.Once);

    /// <summary>
    /// Three answers delivered today on a plan of three: the fourth question stops with the 403
    /// the other plan limits already use, before the cache is read and before the model is called.
    /// </summary>
    [Fact]
    public async Task Handle_OverTheQuotaOfThePlan_ThrowsTheTierLimit_BeforeTheCacheAndTheLlm()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "unused — the question never gets this far");
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 3, max: 3);

        var act = () => BuildHandler(tiers: tiers.Object)
            .Handle(AQuestionFrom(userId, gameId), TestContext.Current.CancellationToken);

        var refusal = (await act.Should().ThrowAsync<Api.SharedKernel.Exceptions.TierLimitExceededException>()).Which;
        (refusal.LimitType, refusal.Current, refusal.Max, refusal.UpgradeUrl).Should().Be(("AgentQuery", 3, 3, "/pricing"));
        _mockResponseCache.Verify(
            c => c.TryGetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockLlmService.Verify(
            s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        NothingWasCounted(tiers);
    }

    [Fact]
    public async Task Handle_AnAnswerFromTheModel_IsCountedOnce()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "The pawn moves forward one space.");
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 2, max: 3);

        var result = await BuildHandler(tiers: tiers.Object)
            .Handle(AQuestionFrom(userId, gameId), TestContext.Current.CancellationToken);

        result.Answer.Should().Be("The pawn moves forward one space.");
        OneAnswerWasCountedFor(userId, tiers);
    }

    /// <summary>An answer served from the cache is a question answered: it counts like any other.</summary>
    [Fact]
    public async Task Handle_AnAnswerFromTheCache_IsCountedToo()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "unused — served from cache");
        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedRagResponse(
                Answer: "Cached answer.",
                Citations: new List<CachedCitation>(),
                ModelUsed: "test-model",
                CachedAt: DateTimeOffset.UtcNow));
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 2, max: 3);

        var result = await BuildHandler(tiers: tiers.Object)
            .Handle(AQuestionFrom(userId, gameId), TestContext.Current.CancellationToken);

        result.Answer.Should().Be("Cached answer.");
        _mockLlmService.Verify(
            s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        OneAnswerWasCountedFor(userId, tiers);
    }

    /// <summary>The gateway stopped the call at the spending cap: no answer, nothing to count.</summary>
    [Fact]
    public async Task Handle_WhenTheGatewayRefuses_NothingIsCounted()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "unused");
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Daily AI budget reached.") with
            {
                Metadata = new Dictionary<string, string> { ["error_code"] = "AI_BUDGET_DAILY_EXHAUSTED" },
            });
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 2, max: 3);

        var act = () => BuildHandler(tiers: tiers.Object)
            .Handle(AQuestionFrom(Guid.NewGuid(), gameId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<LlmCallRefusedException>();
        NothingWasCounted(tiers);
    }

    /// <summary>Nothing was found, so the model was not asked and there is no answer to count.</summary>
    [Fact]
    public async Task Handle_WhenNothingIsFound_NothingIsCounted()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocks(gameId);
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 2, max: 3);

        var result = await BuildHandler(tiers: tiers.Object)
            .Handle(AQuestionFrom(Guid.NewGuid(), gameId), TestContext.Current.CancellationToken);

        result.IsLowQuality.Should().BeTrue();
        _mockLlmService.Verify(
            s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        NothingWasCounted(tiers);
    }

    /// <summary>
    /// The only sender without a user is the auto-test of the admin wizard: no plan to ask, nothing
    /// to count.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutAUser_ThePlanIsNotAsked()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "The pawn moves forward one space.");
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 3, max: 3);

        var result = await BuildHandler(tiers: tiers.Object).Handle(
            new AskQuestionQuery(GameId: gameId, Question: "How does the pawn move?", Language: "en"),
            TestContext.Current.CancellationToken);

        result.Answer.Should().Be("The pawn moves forward one space.");
        tiers.Verify(
            t => t.CheckUsageAsync(
                It.IsAny<Guid>(), It.IsAny<Api.SharedKernel.Services.TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        NothingWasCounted(tiers);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3b (Bug B5 — /agents/qa now threads UserId/UserRole): RAG-access denial
    // throws ForbiddenException before quota consumption and before any LLM call.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRagAccessDenied_ThrowsForbiddenException_WithoutQuotaOrLlm()
    {
        // Arrange — a non-owner (CanAccessRagAsync=false) with an authenticated identity.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        SetupDefaultMocks(gameId);

        var denyRagAccess = new Mock<IRagAccessService>();
        denyRagAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId,
            UserRole: "User");

        // #4168 (5g-b): a plan already used up. Were the quota read first, this user would be told
        // to upgrade a plan for a game they may not ask about at all.
        var tiers = AnswerPipelineTestDoubles.PlanWith(used: 3, max: 3);
        var handler = BuildHandler(ragAccessService: denyRagAccess.Object, tiers: tiers.Object);

        // Act
        var act = () => handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — step 1 (access) precedes step 2 (quota) and the LLM.
        // #4155: the denial is typed and names the game, so the client can offer the declaration.
        (await act.Should().ThrowAsync<RagOwnershipRequiredException>()).Which.GameId.Should().Be(gameId);

        denyRagAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
        tiers.Verify(
            t => t.CheckUsageAsync(
                It.IsAny<Guid>(), It.IsAny<Api.SharedKernel.Services.TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the plan must not even be read when RAG access is denied");
        NothingWasCounted(tiers);
        _mockLlmService.Verify(
            s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "LLM must not be called when RAG access is denied");
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

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(gameId, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(houseRule);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string src, string tgt, CancellationToken _) =>
                TranslationResult.CreateSuccess(text, src, tgt, 0m));

        string? capturedUserPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((r, _) => capturedUserPrompt = r.UserPrompt)
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
    // Test 4b (#3855): la house rule dev'essere AUTORIZZATA nel system prompt
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// #3855 — infilare la regola nel prompt utente non basta, e il difetto lo dimostrava.
    ///
    /// Il system prompt diceva «Answer ONLY using the provided rulebook context» e «Always cite
    /// the page number»: la regola non era fra le fonti autorizzate, quindi il modello la
    /// riformulava («chi ha giocato più di recente» tornava come «chi ha vinto l'ultima partita»),
    /// e l'obbligo di citare senza eccezioni lo portava a inventarle una pagina — [Page 10], che
    /// in quel manuale parla di come mescolare il mazzo.
    ///
    /// Il precedente e' nello stesso metodo: il blocco [Verified Rules] viene autorizzato
    /// esplicitamente. Alla house rule non era mai stato fatto.
    /// </summary>
    [Fact]
    public async Task Handle_WhenHouseRuleMatched_SystemPromptForbidsParaphraseAndPageCitation()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string houseRule = "In our group, the pawn can also move diagonally.";
        const string answer = "Based on your house rule, the pawn moves diagonally.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(gameId, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(houseRule);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string src, string tgt, CancellationToken _) =>
                TranslationResult.CreateSuccess(text, src, tgt, 0m));

        string? capturedSystemPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((r, _) => capturedSystemPrompt = r.SystemPrompt)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: answer,
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "test-model", Provider = "test" }));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        capturedSystemPrompt.Should().NotBeNull();
        capturedSystemPrompt.Should().Contain("[House Rule for this group]",
            "il blocco dev'essere dichiarato fra le fonti autorizzate, altrimenti 'answer ONLY " +
            "using the provided rulebook context' lo esclude e il modello lo riformula");
        capturedSystemPrompt.Should().Contain("do not paraphrase",
            "la regola va riportata com'e': riassumerla ne cambia il significato (#3855)");
        capturedSystemPrompt.Should().Contain("never attach a [Page N] citation",
            "l'obbligo incondizionato di citare una pagina fa inventare una fonte a contenuto " +
            "che non ne ha");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // La cache semantica e le house rule (#3855)
    //
    // La correzione di #3875 vive tutta nell'assemblaggio del system prompt, e i suoi test
    // girano su un cache-miss. Sul percorso di default (`BypassCache = false`) non entra mai in
    // gioco: la chiave e' `rag:cache:{gameId}:*`, senza utente, mentre le house rule sono per
    // utente e i giochi sono condivisi.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Una risposta modellata sulla house rule di un utente non deve finire in una cache
    /// condivisa da tutto il gioco: chi non ha quella regola se la vedrebbe servire come se
    /// fosse il regolamento.
    ///
    /// E' lo stesso trattamento gia' dato ai claim iniettati, per la stessa ragione scritta
    /// sopra la guardia di lettura: la chiave non porta impronta del contenuto iniettato.
    /// </summary>
    [Fact]
    public async Task Handle_WhenHouseRuleMatched_DoesNotWriteToSharedCache()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string houseRule = "In our group, the pawn can also move diagonally.";
        const string answer = "Based on your house rule, the pawn moves diagonally.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(gameId, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(houseRule);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        _mockResponseCache.Verify(
            c => c.SetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CachedRagResponse>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "la risposta cita la house rule verbatim (#3875) e la cache e' per gioco, non per " +
            "utente: scriverla li' la rende servibile a chiunque interroghi lo stesso gioco");
    }

    /// <summary>
    /// Chi ha una house rule non deve nemmeno leggere dalla cache condivisa: una risposta
    /// generica gia' cacheata da un altro utente sopprimerebbe la regola, e il fix di #3875 non
    /// entrerebbe mai in gioco.
    /// </summary>
    [Fact]
    public async Task Handle_WhenHouseRuleMatched_DoesNotReadFromSharedCache()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string houseRule = "In our group, the pawn can also move diagonally.";

        SetupDefaultMocksWithSearchResults(gameId, "The pawn moves forward one space.");

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(gameId, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(houseRule);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        _mockResponseCache.Verify(
            c => c.TryGetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "una risposta generica in cache non deve poter sopprimere la house rule di chi ce l'ha");
    }

    /// <summary>
    /// #4279. Una domanda che nomina un thread riceve nel prompt la cronologia di quella
    /// conversazione. La cache semantica è per gioco, lingua e versione: non conosce la
    /// conversazione. Stessa regola delle house rule e dei claim: ciò che dà forma alla risposta e
    /// non sta nella chiave tiene la risposta fuori dalla cache condivisa.
    ///
    /// Oggi nessun chiamante di produzione passa un thread a questo gestore; il controllo c'è perché
    /// il primo che lo farà non debba saperlo.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheQuestionNamesAThread_DoesNotWriteToSharedCache()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "In your variant the pawn moves twice.");
        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            ThreadId: Guid.NewGuid(),
            Language: "en",
            UserId: Guid.NewGuid());

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        _mockResponseCache.Verify(
            c => c.SetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CachedRagResponse>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "una risposta nata dentro una conversazione non va servita a chi fa la stessa domanda fuori da essa");
    }

    [Fact]
    public async Task Handle_WhenTheQuestionNamesAThread_DoesNotReadFromSharedCache()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "The pawn moves forward one space.");
        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            ThreadId: Guid.NewGuid(),
            Language: "en",
            UserId: Guid.NewGuid());

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        _mockResponseCache.Verify(
            c => c.TryGetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "una domanda di seguito non deve ricevere la risposta in cache di un'altra conversazione");
    }

    /// <summary>
    /// Su cache-hit le pagine citate devono essere quelle vere, non l'indice nella lista.
    ///
    /// E' il sintomo del titolo di #3855 — «le attribuisce a pagine del manuale che non le
    /// contengono» — su un secondo percorso, indipendente dalle house rule e attivo su ogni hit.
    /// `DocumentId` vuoto e `RelevanceScore` a zero sono assenze oneste; un numero di pagina
    /// posizionale e' una fonte inventata, e il frontend lo tratta come coordinata navigabile.
    /// </summary>
    [Fact]
    public async Task Handle_OnCacheHit_DoesNotFabricatePageNumbers()
    {
        var gameId = Guid.NewGuid();

        SetupDefaultMocksWithSearchResults(gameId, "unused — served from cache");

        // tre citazioni: con l'indice posizionale diventerebbero le pagine 1, 2, 3.
        //
        // La terza porta un DocumentId perche' qui si misura la pagina, non la servibilita':
        // senza DocumentId la citazione viene omessa a monte (vedi
        // Handle_OnCacheHit_WithNullDocumentIdCitation_OmitsOnlyThatCitation) e l'assenza di
        // pagina che questo test vuole osservare non arriverebbe mai all'assert.
        var cached = new CachedRagResponse(
            Answer: "Cached answer.",
            Citations: new List<CachedCitation>
            {
                new("Snippet from page 42.", 42, "doc-a"),
                new("Snippet from page 7.", 7, "doc-b"),
                new("Snippet without a page.", null, "doc-c"),
            },
            ModelUsed: "test-model",
            CachedAt: DateTimeOffset.UtcNow);

        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: Guid.NewGuid());

        var result = await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        result.Citations.Should().HaveCount(3);
        // la forma con collezione esplicita, non `Equal(42, 7, null, "perche'")`: quel `params`
        // leggerebbe la motivazione come un quarto elemento atteso
        result.Citations!.Select(c => c.PageNumber).Should().Equal(
            new int?[] { 42, 7, null },
            "le pagine devono venire dalla voce in cache; 1,2,3 significherebbe che sono " +
            "l'indice nella lista");
        result.Citations!.Select(c => c.DocumentId).Should().Equal(
            new string?[] { "doc-a", "doc-b", "doc-c" },
            "il documentId deve venire dalla voce in cache");
    }

    /// <summary>
    /// #4168 (ADR-095 D2): anche una risposta servita dalla cache dice con quale versione e in che
    /// lingua è stata prodotta. La chiave della cache le contiene, quindi sono quelle di adesso.
    /// </summary>
    [Fact]
    public async Task Handle_OnCacheHit_CarriesTheVersionAndTheLanguage()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "unused — served from cache");        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedRagResponse(
                Answer: "Cached answer.",
                Citations: new List<CachedCitation>(),
                ModelUsed: "test-model",
                CachedAt: DateTimeOffset.UtcNow));

        var result = await BuildHandler().Handle(
            new AskQuestionQuery(GameId: gameId, Question: "How does the pawn move?", Language: "en", UserId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Answer.Should().Be("Cached answer.");
        result.ProfileVersionId.Should().Be(AnswerPipelineTestDoubles.SeedVersionId);
        result.Language.Should().Be("en");
    }

    // -------------------------------------------------------------------------
    // Cache-hit: una citazione senza DocumentId non e' servibile
    //
    // `CachedCitation.DocumentId` e' `string?` (#3855: «un'assenza dichiarata e' onesta, mentre
    // un numero inventato e' una fonte falsa che il frontend tratta come coordinata navigabile»)
    // mentre `CitationDto.DocumentId` e' `string`. Il percorso di cache-hit passava il primo al
    // secondo e **non compilava in Release** (CS8604), cioe' l'immagine Docker dell'api non si
    // costruiva. I tre test qui sotto inchiodano la correzione scelta — omettere la citazione —
    // contro le due alternative che avrebbero fatto compilare senza risolvere: `?? ""`, che
    // reintroduce la stringa vuota rimossa di proposito da #3855, e `!`, che e' una promessa al
    // posto di una prova.
    // -------------------------------------------------------------------------

    /// <summary>
    /// La citazione senza DocumentId sparisce; le altre restano, nell'ordine, intatte.
    /// </summary>
    [Fact]
    public async Task Handle_OnCacheHit_WithNullDocumentIdCitation_OmitsOnlyThatCitation()
    {
        var gameId = Guid.NewGuid();

        SetupDefaultMocksWithSearchResults(gameId, "unused - served from cache");

        // la voce centrale e' anteriore a #3855: nessun DocumentId conservato
        var cached = new CachedRagResponse(
            Answer: "Cached answer.",
            Citations: new List<CachedCitation>
            {
                new("Snippet from page 42.", 42, "doc-a"),
                new("Snippet senza documento.", 7, null),
                new("Snippet from page 13.", 13, "doc-c"),
            },
            ModelUsed: "test-model",
            CachedAt: DateTimeOffset.UtcNow);

        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: Guid.NewGuid());

        var result = await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        result.Answer.Should().Be(
            "Cached answer.",
            "la risposta arriva dalla cache: se fosse quella dell'LLM il test non sta " +
            "misurando il ramo di cache-hit");
        result.Citations.Should().HaveCount(2);
        result.Citations!.Select(c => c.DocumentId).Should().Equal(
            new[] { "doc-a", "doc-c" },
            "un DocumentId nullo non e' servibile e la citazione va omessa; una stringa vuota " +
            "al suo posto sarebbe la coordinata inventata che #3855 ha rimosso");
        result.Citations!.Select(c => c.Snippet).Should().NotContain(
            "Snippet senza documento.",
            "la citazione omessa non deve sopravvivere con un altro campo");
        result.Citations!.Select(c => c.PageNumber).Should().Equal(
            new int?[] { 42, 13 },
            "le citazioni superstiti devono restare intatte, non ricompattate su nuovi indici");
    }

    /// <summary>
    /// `RagQueryMetrics.CitationsCount` conta le citazioni **servite**, non quelle conservate.
    ///
    /// E' un cambio di comportamento deliberato: prima la metrica leggeva
    /// `cached.Citations.Count`. Se tornasse a farlo, la dashboard di qualita' conterebbe fonti
    /// che la risposta non contiene.
    /// </summary>
    [Fact]
    public async Task Handle_OnCacheHit_CitationsCountMetric_CountsServedNotStored()
    {
        var gameId = Guid.NewGuid();

        SetupDefaultMocksWithSearchResults(gameId, "unused - served from cache");

        // 4 conservate, 2 servibili
        var cached = new CachedRagResponse(
            Answer: "Cached answer.",
            Citations: new List<CachedCitation>
            {
                new("Con documento.", 1, "doc-a"),
                new("Senza documento.", 2, null),
                new("Documento vuoto.", 3, ""),
                new("Con documento, di nuovo.", 4, "doc-d"),
            },
            ModelUsed: "test-model",
            CachedAt: DateTimeOffset.UtcNow);

        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var trackerMock = new Mock<IRagQualityTracker>();
        var trackedMetrics = new List<RagQueryMetrics>();
        trackerMock
            .Setup(t => t.TrackQueryAsync(It.IsAny<RagQueryMetrics>(), It.IsAny<CancellationToken>()))
            .Callback<RagQueryMetrics, CancellationToken>((m, _) => trackedMetrics.Add(m))
            .Returns(Task.CompletedTask);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: Guid.NewGuid());

        var result = await BuildHandler(qualityTracker: trackerMock.Object)
            .Handle(query, TestContext.Current.CancellationToken);

        // il ramo di cache-hit traccia una volta e ritorna: se ci fossero due metriche il flusso
        // e' proseguito fino all'LLM e la misura sotto non riguarda la cache
        trackedMetrics.Should().HaveCount(1);
        trackedMetrics[0].CacheHit.Should().BeTrue();
        trackedMetrics[0].CitationsCount.Should().Be(
            2,
            "la metrica conta le citazioni servite; 4 significherebbe che e' tornata a leggere " +
            "cached.Citations.Count e conta fonti che la risposta non contiene");
        trackedMetrics[0].CitationsCount.Should().Be(
            result.Citations!.Count,
            "metrica e payload devono raccontare la stessa risposta");

        // la stringa vuota e' trattata come assenza, non come documento di nome ""
        result.Citations!.Select(c => c.DocumentId).Should().Equal(new[] { "doc-a", "doc-d" });
    }

    /// <summary>
    /// La rete contro un filtro troppo avido: con tutti i DocumentId valorizzati non si perde
    /// nulla, ne' nel payload ne' nella metrica.
    /// </summary>
    [Fact]
    public async Task Handle_OnCacheHit_WithAllDocumentIdsPresent_LosesNoCitation()
    {
        var gameId = Guid.NewGuid();

        SetupDefaultMocksWithSearchResults(gameId, "unused - served from cache");

        // include una pagina assente e uno snippet vuoto: solo DocumentId decide la servibilita'
        var cached = new CachedRagResponse(
            Answer: "Cached answer.",
            Citations: new List<CachedCitation>
            {
                new("Snippet A.", 42, "doc-a"),
                new("", 7, "doc-b"),
                new("Snippet C.", null, "doc-c"),
            },
            ModelUsed: "test-model",
            CachedAt: DateTimeOffset.UtcNow);

        _mockResponseCache
            .Setup(c => c.TryGetAsync(gameId, It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var trackerMock = new Mock<IRagQualityTracker>();
        var trackedMetrics = new List<RagQueryMetrics>();
        trackerMock
            .Setup(t => t.TrackQueryAsync(It.IsAny<RagQueryMetrics>(), It.IsAny<CancellationToken>()))
            .Callback<RagQueryMetrics, CancellationToken>((m, _) => trackedMetrics.Add(m))
            .Returns(Task.CompletedTask);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: Guid.NewGuid());

        var result = await BuildHandler(qualityTracker: trackerMock.Object)
            .Handle(query, TestContext.Current.CancellationToken);

        result.Citations.Should().HaveCount(
            3,
            "il filtro guarda solo DocumentId: una pagina assente o uno snippet vuoto non " +
            "rendono la citazione inservibile");
        result.Citations!.Select(c => c.DocumentId).Should().Equal(new[] { "doc-a", "doc-b", "doc-c" });
        result.Citations!.Select(c => c.PageNumber).Should().Equal(new int?[] { 42, 7, null });
        trackedMetrics.Should().HaveCount(1);
        trackedMetrics[0].CitationsCount.Should().Be(3);
    }

    /// <summary>
    /// Senza house rule il system prompt non deve cambiare: l'istruzione e' condizionale, non
    /// una zavorra su ogni richiesta.
    /// </summary>
    [Fact]
    public async Task Handle_WhenNoHouseRule_SystemPromptUnchanged()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string answer = "The pawn moves forward one space.";

        SetupDefaultMocksWithSearchResults(gameId, answer);

        _mockHouseRuleMatcher
            .Setup(m => m.FindMatchingHouseRuleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockTranslationService
            .Setup(t => t.TranslateGenericAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string src, string tgt, CancellationToken _) =>
                TranslationResult.CreateSuccess(text, src, tgt, 0m));

        string? capturedSystemPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((r, _) => capturedSystemPrompt = r.SystemPrompt)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: answer,
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "test-model", Provider = "test" }));

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: "How does the pawn move?",
            Language: "en",
            UserId: userId);

        await BuildHandler().Handle(query, TestContext.Current.CancellationToken);

        capturedSystemPrompt.Should().NotBeNull();
        capturedSystemPrompt.Should().NotContain("[House Rule for this group]");
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

    // ── R1 (issue #3416, ADR-088): MechanicCard claim injection ─────────────────────────────────
    // This class's _searchHandler is wired to always return EMPTY results, so it exercises the exact
    // retrieval-miss case R1 targets (e.g. TM "Setup per N"): flag off → sentinel; flag on + card →
    // the claim block is injected above the no-results early-exit and the LLM is invoked.

    private const string NoContextSentinel = "This information is not available in the provided rulebook.";

    private static PublishedMechanicCardDto CardWithSetupClaim(Guid gameId, Guid pdfId) => new(
        CardId: Guid.NewGuid(),
        SharedGameId: gameId,
        Title: "T",
        Version: 1,
        PublishedAt: DateTime.UtcNow,
        GameName: "Terraforming Mars",
        Publisher: null,
        Language: "it",
        Sections: new[]
        {
            new PublishedMechanicCardSectionDto("Setup", new[]
            {
                new PublishedMechanicCardClaimDto(
                    Guid.NewGuid(),
                    "In una partita a 3 giocatori si usa la plancia standard.",
                    new[] { new PublishedMechanicCardCitationDto(pdfId, 3, "3-player uses the standard board") }),
            }),
        },
        SourceAnalysisId: Guid.NewGuid(),
        PublicationYear: null,
        DocumentName: null);

    private void SetupSuccessfulLlm(out System.Collections.Generic.List<string> capturedUserPrompts)
    {
        var captured = new System.Collections.Generic.List<string>();
        capturedUserPrompts = captured;
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((r, _) => captured.Add(r.UserPrompt))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: "In una partita a 3 si usa la plancia standard. [Page 3]",
                usage: new LlmUsage(1, 1, 2),
                cost: new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "test", Provider = "test" }));        // QualityTrackingDomainService is mocked in this class → its virtual confidence methods return
        // null by default; the handler dereferences .Value on each. Return Zero for the full LLM path.
        _mockQualityService
            .Setup(q => q.CalculateSearchConfidence(It.IsAny<System.Collections.Generic.List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>()))
            .Returns(Confidence.Zero);
        _mockQualityService
            .Setup(q => q.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<System.Collections.Generic.List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult>>()))
            .Returns(Confidence.Zero);
        _mockQualityService
            .Setup(q => q.CalculateOverallConfidence(It.IsAny<Confidence>(), It.IsAny<Confidence>()))
            .Returns(Confidence.Zero);
    }

    [Fact]
    public async Task Handle_FlagOnWithCard_InjectsVerifiedRulesBlock_WhenRetrievalEmpty()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        SetupSuccessfulLlm(out var capturedPrompts);

        var providerMock = new Mock<IMechanicCardProvider>();
        providerMock
            .Setup(p => p.GetActiveCardAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CardWithSetupClaim(gameId, pdfId));

        var flagMock = new Mock<IFeatureFlagService>();
        flagMock
            .Setup(f => f.IsEnabledAsync(FeatureFlagConstants.MechanicCardInjectionKey, It.IsAny<UserRole?>()))
            .ReturnsAsync(true);

        var handler = BuildHandler(mechanicCardProvider: providerMock.Object, featureFlags: flagMock.Object);
        var query = new AskQuestionQuery(GameId: gameId, Question: "Setup per 3 giocatori", UserId: Guid.NewGuid(), UserRole: "User");

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Bypassed the no-results early-exit — the LLM ran and received the authoritative block.
        result.Answer.Should().NotBe(NoContextSentinel);
        capturedPrompts.Should().ContainSingle();
        capturedPrompts[0].Should().Contain("[Verified Rules — human-approved]");
        capturedPrompts[0].Should().Contain("## Setup");
        capturedPrompts[0].Should().Contain("In una partita a 3 giocatori si usa la plancia standard.");
        // Verbatim quote must NOT be in the prompt body (copyright rule §7.2).
        capturedPrompts[0].Should().NotContain("3-player uses the standard board");
        // Claim citation surfaced (PdfPage + verbatim Quote).
        result.Citations.Should().Contain(c => c.PageNumber == 3 && c.Snippet == "3-player uses the standard board");
    }

    [Fact]
    public async Task Handle_FlagOff_ReturnsSentinel_WhenRetrievalEmpty()
    {
        var gameId = Guid.NewGuid();
        SetupSuccessfulLlm(out _);

        // BuildHandler's default feature-flag mock returns false → no injection.
        var handler = BuildHandler();
        var query = new AskQuestionQuery(GameId: gameId, Question: "Setup per 3 giocatori", UserId: Guid.NewGuid(), UserRole: "User");

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        result.Answer.Should().Be(NoContextSentinel);
        _mockLlmService.Verify(
            s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private AskQuestionQueryHandler BuildHandler(
        IRagAccessService? ragAccessService = null,
        Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection.IMechanicCardProvider? mechanicCardProvider = null,
        Api.Services.IFeatureFlagService? featureFlags = null,
        IRagQualityTracker? qualityTracker = null,
        Api.SharedKernel.Services.ITierEnforcementService? tiers = null) =>
        new(
            AnswerPipelineTestDoubles.Retrieval(_searchHandler, CreatePassthroughReranker()),
            AnswerPipelineTestDoubles.ProfileReader(),
            AnswerPipelineTestDoubles.UncertainLanguageDetection(),
            _mockQualityService.Object,
            _mockChatContextService.Object,
            _mockThreadRepository.Object,
            _mockPdfDocumentRepository.Object,
            _mockLlmService.Object,
            _mockValidationPipeline.Object,
            Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Preconditions(ragAccessService ?? CreatePermissiveRagAccessServiceMock(), tiers),
            qualityTracker ?? Mock.Of<IRagQualityTracker>(),
            new QueryComplexityAnalyzer(),
            _mockResponseCache.Object,
            _mockEmbeddingService.Object,
            _mockHouseRuleMatcher.Object,
            _mockTranslationService.Object,
            // D7: use the real classifier (pure, stateless, no dependencies).
            new IntentClassifierService(),
            mechanicCardProvider ?? Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection.IMechanicCardProvider>(),
            featureFlags ?? Mock.Of<Api.Services.IFeatureFlagService>(),
            _mockLogger.Object);

    private static Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.ICrossEncoderReranker CreatePassthroughReranker()
    {
        var mock = new Mock<Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.ICrossEncoderReranker>();
        mock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.RerankChunk>>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.RerankChunk> chunks, int? topK, CancellationToken _) =>
                new Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.RerankResult(
                    chunks.Take(topK ?? chunks.Count)
                        .Select((c, i) => new Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking.RerankedChunk(c.Id, c.Content, c.OriginalScore, 0.9 - (i * 0.1)))
                        .ToList(),
                    "test-model", 1.0));
        return mock.Object;
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

        _mockValidationPipeline
            .Setup(v => v.ValidateResponseAsync(
                It.IsAny<Api.Models.QaResponse>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagValidationResult
            {
                IsValid = true,
                LayersPassed = 3,
                TotalLayers = 3,
                ConfidenceValidation = new ConfidenceValidationResult
                {
                    IsValid = true,
                    ValidationMessage = "Confidence is acceptable",
                    Severity = ValidationSeverity.Pass,
                    ActualConfidence = 0.8,
                    RequiredThreshold = 0.7
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
                It.IsAny<int>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Returns(oneResult);

        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
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
    // #4168 — il modello e i parametri vengono dalla versione pubblicata del profilo
    //
    // Sostituisce i test di #562: l'override del modello per livello di complessità
    // (`LlmRouting:QueryComplexityModels`) è stato tolto. Era configurato vuoto, quindi non cambiava
    // nulla, e con la pipeline il modello lo decide solo il profilo.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("What is the winning condition?")]
    [InlineData("Explain the full interaction between the trading phase, the robber and the longest road bonus in a 4-player game")]
    public async Task Handle_WhateverTheComplexity_AsksTheGatewayForTheModelOfThePublishedProfile(string question)
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "Answer [Page 1]");
        LlmAnswerRequest? sent = null;
        _mockLlmService
            .Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                response: "Answer [Page 1]",
                usage: new LlmUsage(10, 10, 20),
                cost: new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "deepseek-flash", Provider = "DeepSeek" }));

        await BuildHandler().Handle(
            new AskQuestionQuery(GameId: gameId, Question: question, UserId: Guid.NewGuid(), UserRole: "User"),
            TestContext.Current.CancellationToken);

        sent.Should().NotBeNull();
        sent!.PrimaryModelId.Should().Be("deepseek-flash");
        sent.FallbackModelId.Should().Be("meta-llama/llama-3.3-70b-instruct");
        sent.ProfileVersionId.Should().Be(AnswerPipelineTestDoubles.SeedVersionId);
    }

    /// <summary>
    /// #4168: un rifiuto del gateway diventava una risposta vuota, servita come riuscita. Ora è un
    /// errore con il codice del gateway, e nella cache non finisce niente.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheGatewayRefuses_ThrowsWithTheCode_AndCachesNothing()
    {
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "unused");
        var refusal = LlmCompletionResult.CreateFailure("Daily AI budget reached.") with
        {
            Metadata = new Dictionary<string, string> { ["error_code"] = "AI_BUDGET_DAILY_EXHAUSTED" },
        };
        _mockLlmService.Setup(s => s.GenerateAnswerAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(refusal);

        var act = () => BuildHandler().Handle(
            new AskQuestionQuery(GameId: gameId, Question: "How do I win?", UserId: Guid.NewGuid(), UserRole: "User"),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<LlmCallRefusedException>()).Which.ErrorCode.Should().Be("AI_BUDGET_DAILY_EXHAUSTED");
        _mockResponseCache.Verify(
            c => c.SetAsync(It.IsAny<Guid>(), It.IsAny<SemanticCacheScope>(), It.IsAny<float[]>(), It.IsAny<CachedRagResponse>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("it", "it")]
    public async Task Handle_TheSemanticCache_IsScopedToThePublishedVersionAndTheAnswerLanguage(string? requested, string expected)
    {
        // #4168: la cache semantica e' per gioco e per somiglianza; l'ambito (versione del profilo,
        // lingua della risposta) sta nella chiave, cosi' una risposta di un'altra versione o lingua
        // non si trova nemmeno — e non nasconde quelle buone.
        var gameId = Guid.NewGuid();
        SetupDefaultMocksWithSearchResults(gameId, "Fresh answer [Page 1]");
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 1f, 0f } }));

        await BuildHandler().Handle(
            new AskQuestionQuery(GameId: gameId, Question: "How do I win?", Language: requested, UserId: Guid.NewGuid(), UserRole: "User"),
            TestContext.Current.CancellationToken);

        var scope = new SemanticCacheScope(AnswerPipelineTestDoubles.SeedVersionId, expected);
        _mockResponseCache.Verify(c => c.TryGetAsync(gameId, scope, It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockResponseCache.Verify(
            c => c.SetAsync(gameId, scope, It.IsAny<float[]>(), It.IsAny<CachedRagResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
