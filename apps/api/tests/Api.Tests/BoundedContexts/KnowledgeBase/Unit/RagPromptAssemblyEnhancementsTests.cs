using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SearchResultItem = Api.Services.SearchResultItem;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagPromptAssemblyEnhancementsTests
{
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IEmbeddingRepository> _embeddingRepositoryMock;
    private readonly Mock<ICrossEncoderReranker> _rerankerMock;
    private readonly Mock<ILlmService> _llmMock;
    private readonly Mock<ITextChunkSearchService> _textSearchMock;
    private readonly Mock<IExpansionGameResolver> _expansionResolverMock;
    private readonly Mock<IRagEnhancementService> _ragEnhancementMock;
    private readonly Mock<IQueryComplexityClassifier> _complexityClassifierMock;
    private readonly Mock<IRetrievalRelevanceEvaluator> _relevanceEvaluatorMock;
    private readonly Mock<IQueryExpander> _queryExpanderMock;
    private readonly Mock<IGraphRetrievalService> _graphRetrievalMock;
    private readonly Mock<ILogger<RagPromptAssemblyService>> _loggerMock;

    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly float[] TestEmbedding = [0.1f, 0.2f, 0.3f];

    public RagPromptAssemblyEnhancementsTests()
    {
        _embeddingMock = new Mock<IEmbeddingService>();
        _embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        _rerankerMock = new Mock<ICrossEncoderReranker>();
        _llmMock = new Mock<ILlmService>();
        _textSearchMock = new Mock<ITextChunkSearchService>();
        _expansionResolverMock = new Mock<IExpansionGameResolver>();
        _ragEnhancementMock = new Mock<IRagEnhancementService>();
        _complexityClassifierMock = new Mock<IQueryComplexityClassifier>();
        _relevanceEvaluatorMock = new Mock<IRetrievalRelevanceEvaluator>();
        _queryExpanderMock = new Mock<IQueryExpander>();
        _graphRetrievalMock = new Mock<IGraphRetrievalService>();
        _loggerMock = new Mock<ILogger<RagPromptAssemblyService>>();

        // Default: query expansion returns empty (no expansions)
        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = false });

        // Default: no expansion games
        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        // Default: successful embedding
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([TestEmbedding]));

        // Default: empty text search (no FTS results)
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>());

        // Default: empty vector search (no embedding matches)
        _embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoredEmbedding>());
    }

    private RagPromptAssemblyService CreateService()
    {
        return new RagPromptAssemblyService(
            _embeddingMock.Object,
            _embeddingRepositoryMock.Object,
            _rerankerMock.Object,
            _llmMock.Object,
            _textSearchMock.Object,
            _expansionResolverMock.Object,
            _ragEnhancementMock.Object,
            _complexityClassifierMock.Object,
            _relevanceEvaluatorMock.Object,
            _queryExpanderMock.Object,
            _graphRetrievalMock.Object,
            _loggerMock.Object);
    }

    private void SetupTextSearchResults(params SearchResultItem[] items)
    {
        var ftsResults = items.Select(i => new TextChunkMatch(
            PdfDocumentId: Guid.TryParse(i.PdfId, out var pid) ? pid : Guid.NewGuid(),
            Content: i.Text,
            ChunkIndex: i.ChunkIndex,
            PageNumber: i.Page,
            Rank: i.Score)).ToList();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ftsResults);
    }

    private void SetupRerankerPassthrough()
    {
        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
            {
                var reranked = chunks.Take(topK ?? chunks.Count)
                    .Select((c, i) => new RerankedChunk(c.Id, c.Content, 0.9 - (i * 0.1), c.OriginalScore))
                    .ToList();
                return new RerankResult(reranked, "test-model", 10.0);
            });
    }

    private static SearchResultItem CreateChunk(string pdfId, int chunkIndex, float score, string text = "Rule text", int page = 1)
    {
        return new SearchResultItem
        {
            Score = score,
            Text = text,
            PdfId = pdfId,
            Page = page,
            ChunkIndex = chunkIndex
        };
    }

    #region Adaptive Routing

    [Fact]
    public async Task WhenAdaptiveRouting_SimpleQuery_SkipsRetrieval()
    {
        // Arrange: Adaptive routing classifies query as Simple, so retrieval is skipped entirely
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.AdaptiveRouting);

        _complexityClassifierMock
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryComplexity.Simple("Greeting query", 0.95f));

        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Some rule text"));
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "Hello!",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: Search services should NOT be called when adaptive routing skips retrieval
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _textSearchMock.Verify(
            t => t.FullTextSearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.Citations.Should().BeEmpty();
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
    }

    #endregion

    #region CRAG Evaluation

    [Fact]
    public async Task WhenCragEvaluation_IncorrectChunks_RequeriesWithExpansion()
    {
        // Arrange: CRAG evaluates initial retrieval as Incorrect, triggering expanded retrieval
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.CragEvaluation);

        // CRAG evaluation returns Incorrect verdict, should replace results
        _relevanceEvaluatorMock
            .Setup(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.85f, "Results not relevant"));

        // Use duplicate FTS entries to push RRF scores above the 0.55 threshold
        var docId = Guid.NewGuid();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>
            {
                new(docId, "Initial chunk text", 0, 1, 0.90f),
                new(docId, "Initial chunk text", 0, 1, 0.85f), // duplicate boosts RRF above threshold
            });
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How does castling work?",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: CRAG evaluation should be called with the initial chunks
        _relevanceEvaluatorMock.Verify(
            e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // FTS should be called at least twice: initial search + CRAG expanded search
        _textSearchMock.Verify(
            t => t.FullTextSearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    #endregion

    #region RAG-Fusion

    [Fact]
    public async Task WhenRagFusion_ExpandsQueryVariants()
    {
        // Arrange: RAG-Fusion uses IQueryExpander instead of the default LLM-based expansion
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.RagFusionQueries);

        _queryExpanderMock
            .Setup(q => q.ExpandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>
            {
                "How do pawns move?",
                "pawn movement rules chess",
                "rules for moving pawns forward"
            });

        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: IQueryExpander should be used (not the default LLM-based expansion)
        _queryExpanderMock.Verify(
            q => q.ExpandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // The default LLM-based query expansion should NOT be called
        _llmMock.Verify(
            l => l.GenerateCompletionAsync(
                It.Is<string>(s => s.Contains("query expansion")),
                It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Embeddings should be generated for all 3 query variants
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    #endregion

    #region RAPTOR Retrieval

    [Fact]
    public async Task WhenRaptorRetrieval_AddsSummaryChunksToContext()
    {
        // Arrange: RAPTOR retrieval adds hierarchical summary chunks to the pipeline
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.RaptorRetrieval);

        // Use duplicate FTS entries to push RRF scores above the 0.55 threshold
        var docId = Guid.NewGuid();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>
            {
                new(docId, "Pawns move forward one square.", 0, 1, 0.90f),
                new(docId, "Pawns move forward one square.", 0, 1, 0.85f), // duplicate boosts RRF above threshold
            });
        SetupRerankerPassthrough();

        var raptorDocId = Guid.NewGuid();
        _textSearchMock
            .Setup(t => t.SearchRaptorSummariesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>
            {
                new(raptorDocId, "Summary: Movement rules overview for all pieces.", 100, 1, 0.80f),
                new(raptorDocId, "Summary: Pawn-specific rules including en passant.", 101, 2, 0.75f)
            });

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: RAPTOR search should be called
        _textSearchMock.Verify(
            t => t.SearchRaptorSummariesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Result should contain chunks from both standard and RAPTOR retrieval
        result.Citations.Should().NotBeEmpty();
        result.SystemPrompt.Should().Contain("Movement rules overview");
    }

    [Fact]
    public async Task WhenRaptorRetrieval_NotActive_DoesNotCallRaptorSearch()
    {
        // Arrange: No RAPTOR flag means SearchRaptorSummariesAsync should not be called
        var tier = UserTier.Free;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.None);

        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: RAPTOR search should NOT be called
        _textSearchMock.Verify(
            t => t.SearchRaptorSummariesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region No Enhancements / Backward Compatibility

    [Fact]
    public async Task WhenNoEnhancements_PipelineBehavesAsOriginal()
    {
        // Arrange: No enhancements active; pipeline should behave identically to pre-enhancement version
        var tier = UserTier.Free;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.None);

        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None);

        // Assert: Enhancement services should NOT be called
        _complexityClassifierMock.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _relevanceEvaluatorMock.Verify(
            e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _queryExpanderMock.Verify(
            q => q.ExpandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Standard pipeline should still run (embedding + search)
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _textSearchMock.Verify(
            t => t.FullTextSearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenUserTierNull_SkipsEnhancements()
    {
        // Arrange: null tier means no enhancement lookup at all
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None);

        // Assert: Enhancement service should NOT be called when tier is null
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _complexityClassifierMock.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _relevanceEvaluatorMock.Verify(
            e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _queryExpanderMock.Verify(
            q => q.ExpandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Standard pipeline should still run
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenLiveSessionPolicy_SkipsEnhancementsEvenWithTier()
    {
        // #3389: the in-session live path is explicitly decoupled from tier. Even with a tier present
        // (which WOULD activate enhancements), RetrievalPolicy.LiveSession (EnhancementsEnabled=false)
        // must NOT look up tier-derived enhancements — gating is an explicit capability, not a
        // side-effect of the tier being null/non-null.
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.AdaptiveRouting);
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act — explicit LiveSession policy alongside a tier that WOULD otherwise enable enhancements
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.LiveSession);

        // Assert — enhancements gated off explicitly: no tier lookup, no adaptive routing
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _complexityClassifierMock.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Tier-independent EnabledEnhancements set (#3390 Slice 4 / D1)

    [Fact]
    public async Task WhenEnabledEnhancementsSet_ActivatesWithoutTier()
    {
        // #3390 D1: an explicit EnabledEnhancements set activates enhancements with NO tier at all —
        // grounding is a correctness property, not a tier perk. Here AdaptiveRouting is requested via
        // the policy set (userTier=null); the complexity classifier must run.
        _complexityClassifierMock
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryComplexity.Simple("Greeting query", 0.95f));
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act — explicit set, tier null
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "Hello!",
            TestGameId, null, userTier: null, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.LiveSessionWith(RagEnhancement.AdaptiveRouting));

        // Assert — the enhancement activated from the explicit set, without any tier lookup
        _complexityClassifierMock.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenEnabledEnhancementsSet_BypassesTierLookup()
    {
        // #3390 D1: the explicit set wins over the tier path — GetActiveEnhancementsAsync is never
        // consulted even when a tier is present. Here CRAG is requested via the set; its evaluator runs.
        var tier = UserTier.Premium;
        _relevanceEvaluatorMock
            .Setup(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "Relevant"));
        var docId = Guid.NewGuid();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>
            {
                new(docId, "Initial chunk text", 0, 1, 0.90f),
                new(docId, "Initial chunk text", 0, 1, 0.85f), // duplicate boosts RRF above threshold
            });
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act — explicit CRAG set AND a tier that would otherwise drive the tier path
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How does castling work?",
            TestGameId, null, tier, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.LiveSessionWith(RagEnhancement.CragEvaluation));

        // Assert — CRAG ran from the explicit set; the tier lookup was bypassed entirely
        _relevanceEvaluatorMock.Verify(
            e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenEnabledEnhancementsNull_LegacyTierPathUnchanged()
    {
        // #3390 D1 backward-compat: a policy with EnabledEnhancements=null keeps the legacy behaviour —
        // enhancements resolved from the tier via GetActiveEnhancementsAsync. RetrievalPolicy.Default
        // carries EnhancementsEnabled=true and no explicit set, so the tier path runs.
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.None);
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act — explicit-null set + a tier → legacy tier path
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.Default);

        // Assert — the tier lookup ran (legacy path preserved)
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CRAG web-fallback OFF on the closed rulebook domain (#3467)

    [Fact]
    public async Task WhenLiveSessionPolicy_CragEvaluatorNeverInvoked()
    {
        // #3467 negative-space assertion: on the in-session live path the CRAG evaluator must NOT run —
        // even when the user's tier would activate CRAG — because RetrievalPolicy.LiveSession gates
        // enhancements OFF (EnhancementsEnabled=false). This is the trust-critical invariant the grounding
        // audit (§7) flagged as invisible to CI. It guards the #3389 wiring against regression.
        var tier = UserTier.Premium;
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.CragEvaluation);
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act — live path alongside a tier whose enhancements include CRAG
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, tier, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.LiveSession);

        // Assert — CRAG evaluation never runs on the live path, and the tier is never even consulted
        _relevanceEvaluatorMock.Verify(
            e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScoredChunk>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _ragEnhancementMock.Verify(
            r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RagPromptAssemblyService_HasNoWebRetrievalDependency()
    {
        // #3467 negative-space assertion: the CRAG "corrective" requery on the closed rulebook domain
        // stays internal to the corpus (ExpandQueryAsync + TryHybridSearchAsync). The forward-looking
        // web-fallback plugins (RetrievalWebPlugin / EvaluationCragPlugin, which simulate "web_search")
        // must NEVER be wired into the live prompt-assembly path. If #3390 later routes an action here to
        // a web plugin, this test goes RED — forcing an explicit new flag that keeps the rulebook domain OFF.
        var ctor = typeof(RagPromptAssemblyService).GetConstructors().Single();
        var parameterTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        parameterTypes.Should().NotContain(typeof(RetrievalWebPlugin));
        parameterTypes.Should().NotContain(typeof(EvaluationCragPlugin));
        parameterTypes.Select(t => t.Name).Should().NotContain(
            n => n.Contains("WebPlugin", StringComparison.Ordinal),
            "web-fallback retrieval must not be a dependency of the live prompt-assembly path");
    }

    #endregion
}
