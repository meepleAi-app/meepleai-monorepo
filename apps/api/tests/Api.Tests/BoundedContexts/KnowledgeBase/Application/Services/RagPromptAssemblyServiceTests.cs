using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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
using SearchResult = Api.Services.SearchResult;
using SearchResultItem = Api.Services.SearchResultItem;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagPromptAssemblyServiceTests
{
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<ICrossEncoderReranker> _rerankerMock;
    private readonly Mock<ILlmService> _llmMock;
    private readonly Mock<ITextChunkSearchService> _textSearchMock;
    private readonly Mock<IExpansionGameResolver> _expansionResolverMock;
    private readonly Mock<IRagEnhancementService> _ragEnhancementMock;
    private readonly Mock<IQueryComplexityClassifier> _complexityClassifierMock;
    private readonly Mock<IRetrievalRelevanceEvaluator> _relevanceEvaluatorMock;
    private readonly Mock<IQueryExpander> _queryExpanderMock;
    private readonly Mock<ILogger<RagPromptAssemblyService>> _loggerMock;

    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly float[] TestEmbedding = [0.1f, 0.2f, 0.3f];

    public RagPromptAssemblyServiceTests()
    {
        _embeddingMock = new Mock<IEmbeddingService>();
        _rerankerMock = new Mock<ICrossEncoderReranker>();
        _llmMock = new Mock<ILlmService>();
        _textSearchMock = new Mock<ITextChunkSearchService>();
        _expansionResolverMock = new Mock<IExpansionGameResolver>();
        _ragEnhancementMock = new Mock<IRagEnhancementService>();
        _complexityClassifierMock = new Mock<IQueryComplexityClassifier>();
        _relevanceEvaluatorMock = new Mock<IRetrievalRelevanceEvaluator>();
        _queryExpanderMock = new Mock<IQueryExpander>();
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

        // Default: no RAG enhancements active
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.None);
    }

    private RagPromptAssemblyService CreateService()
    {
        return new RagPromptAssemblyService(
            _embeddingMock.Object,
            _rerankerMock.Object,
            _llmMock.Object,
            _textSearchMock.Object,
            _expansionResolverMock.Object,
            _ragEnhancementMock.Object,
            _complexityClassifierMock.Object,
            _relevanceEvaluatorMock.Object,
            _queryExpanderMock.Object,
            _loggerMock.Object);
    }

    private void SetupSuccessfulEmbedding()
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([TestEmbedding]));
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

    #region AssemblePromptAsync - With Chunks (FTS-only after Qdrant removal)

    [Fact]
    public async Task AssemblePrompt_WithChunks_FtsOnlyScoresBelowThreshold_ReturnsEmptyContext()
    {
        // Arrange — After Qdrant removal, FTS results go through RRF scoring.
        // With no vector results, RRF-normalized FTS scores max out at ~0.49,
        // which is below the DefaultMinScore (0.55), so all chunks are filtered out.
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk("doc1", 0, 0.90f, "Pawns move forward one square."),
            CreateChunk("doc1", 1, 0.85f, "Pawns can capture diagonally."),
            CreateChunk("doc2", 0, 0.80f, "The king can move one square in any direction."),
            CreateChunk("doc2", 1, 0.75f, "Castling requires neither king nor rook having moved."),
            CreateChunk("doc3", 0, 0.70f, "Knights move in an L-shape.")
        );
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert — No chunks pass the 0.55 threshold with FTS-only RRF scores
        result.Should().NotBeNull();
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
        result.EstimatedTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssemblePrompt_WithChunks_FtsOnlyScoresBelowThreshold_CreateNoCitations()
    {
        // Arrange — After Qdrant removal, FTS-only RRF scores are below MinScore threshold.
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk("doc1", 0, 0.90f, "Pawns move forward one square.", page: 5),
            CreateChunk("doc2", 0, 0.85f, "Knights move in L-shape.", page: 12)
        );
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pieces move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert — No citations because FTS-only RRF scores are below 0.55
        result.Citations.Should().BeEmpty();
    }

    #endregion

    #region AssemblePromptAsync - No Chunks

    [Fact]
    public async Task AssemblePrompt_WithNoChunks_IncludesDisclaimer()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(); // empty results
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "arbitro", "Chess", null, "What is en passant?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task AssemblePrompt_WithLowScoreChunks_ReturnsEmptyContext()
    {
        // Arrange - all chunks below minScore (0.55)
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk("doc1", 0, 0.30f, "Irrelevant text"),
            CreateChunk("doc1", 1, 0.40f, "Also irrelevant")
        );
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
    }

    #endregion

    #region AssemblePromptAsync - Chat History

    [Fact]
    public async Task AssemblePrompt_WithChatHistory_IncludesMessages()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawns move forward."));
        SetupRerankerPassthrough();

        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), gameId: TestGameId, title: "Chat about chess", agentType: "tutor");
        thread.AddUserMessage("How do pawns move?");
        thread.AddAssistantMessageWithMetadata("Pawns move forward one square.", "tutor", 50);
        thread.AddUserMessage("Can they capture?");

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "Can they capture?",
            TestGameId, thread, null, CancellationToken.None);

        // Assert
        result.UserPrompt.Should().Contain("Conversation History");
        result.UserPrompt.Should().Contain("How do pawns move?");
        result.UserPrompt.Should().Contain("Pawns move forward one square.");
        result.UserPrompt.Should().Contain("Can they capture?");
    }

    [Fact]
    public async Task AssemblePrompt_WithLongHistory_UsesSummary()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();

        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), gameId: TestGameId, title: "Long chat", agentType: "tutor");

        // Add > HistoryThreshold (10) messages
        for (var i = 0; i < 15; i++)
        {
            thread.AddUserMessage($"Question {i}");
            thread.AddAssistantMessageWithMetadata($"Answer {i}", "tutor", 20);
        }

        // Set conversation summary
        thread.UpdateConversationSummary("This conversation covered chess pawn movement rules extensively.");

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "Final question",
            TestGameId, thread, null, CancellationToken.None);

        // Assert
        result.UserPrompt.Should().Contain("[Previous conversation summary]");
        result.UserPrompt.Should().Contain("chess pawn movement rules extensively");
        result.UserPrompt.Should().Contain("[Recent messages]");
        // Should include recent messages but not all 30
        result.UserPrompt.Should().Contain("Final question");
    }

    [Fact]
    public async Task AssemblePrompt_WithNullThread_OmitsHistory()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.UserPrompt.Should().NotContain("Conversation History");
        result.UserPrompt.Should().Contain("Current Question");
        result.UserPrompt.Should().Contain("How do pawns move?");
    }

    #endregion

    #region AssemblePromptAsync - Embedding Failure

    [Fact]
    public async Task AssemblePrompt_EmbeddingFails_ReturnsEmptyContext()
    {
        // Arrange
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Service unavailable"));
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
    }

    #endregion

    #region AssemblePromptAsync - Text Search Failure

    [Fact]
    public async Task AssemblePrompt_TextSearchReturnsEmpty_ReturnsGracefulError()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>());
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task AssemblePrompt_TextSearchThrows_ReturnsGracefulError()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert - graceful degradation, no exception thrown
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
        result.Citations.Should().BeEmpty();
    }

    #endregion

    #region AssemblePromptAsync - Reranker Failure

    [Fact]
    public async Task AssemblePrompt_RerankerFails_FtsOnlyScoresBelowThreshold_ReturnsEmptyContext()
    {
        // Arrange — After Qdrant removal, FTS results go through RRF scoring only.
        // RRF-normalized FTS scores are below the 0.55 threshold, so reranker
        // is never reached (no chunks pass the filter to be reranked).
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk("doc1", 0, 0.90f, "High score chunk"),
            CreateChunk("doc1", 1, 0.85f, "Second chunk"),
            CreateChunk("doc2", 0, 0.80f, "Third chunk"),
            CreateChunk("doc2", 1, 0.75f, "Fourth chunk"),
            CreateChunk("doc3", 0, 0.70f, "Fifth chunk"),
            CreateChunk("doc3", 1, 0.65f, "Sixth chunk")
        );
        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Reranker service down"));
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pieces move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert — FTS-only RRF scores are below threshold, so no results regardless of reranker
        result.Citations.Should().BeEmpty();
        result.SystemPrompt.Should().Contain("No game documentation is currently available");
    }

    #endregion

    #region AssemblePromptAsync - Game State

    [Fact]
    public async Task AssemblePrompt_WithGameState_IncludesStateInPrompt()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();

        var playerId = Guid.NewGuid();
        var gameState = GameState.Create(
            currentTurn: 5,
            activePlayer: playerId,
            playerScores: new Dictionary<Guid, decimal> { [playerId] = 0m },
            gamePhase: "Middlegame",
            lastAction: "Moved bishop to c4");
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "stratega", "Chess", gameState, "What should I do?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("Current Game State");
        result.SystemPrompt.Should().Contain("Turn: 5");
        result.SystemPrompt.Should().Contain($"Active player: {playerId}");
        result.SystemPrompt.Should().Contain("Phase: Middlegame");
        result.SystemPrompt.Should().Contain("Last action: Moved bishop to c4");
    }

    #endregion

    #region AssemblePromptAsync - Persona

    [Fact]
    public async Task AssemblePrompt_WithAgentTypology_IncludesPersona()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "arbitro", "Catan", null, "Is this legal?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("arbitro");
        result.SystemPrompt.Should().Contain("Catan");
    }

    #endregion

    #region ComputeConfidence

    [Fact]
    public void ComputeConfidence_WithNoCitations_ReturnsNull()
    {
        var result = RagPromptAssemblyService.ComputeConfidence([], "Some response");
        result.Should().BeNull();
    }

    [Fact]
    public void ComputeConfidence_WithHighScoreChunks_ReturnsHighConfidence()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc1", 1, 0.95f, "High relevance chunk"),
            new("doc2", 2, 0.90f, "Also high relevance")
        };

        var result = RagPromptAssemblyService.ComputeConfidence(citations, "Clear definitive answer.");

        result.Should().NotBeNull();
        result!.Value.Should().BeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public void ComputeConfidence_WithLowScoreChunks_AppliesPenalty()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc1", 1, 0.60f, "Low relevance"),
            new("doc2", 2, 0.65f, "Also low")
        };

        var result = RagPromptAssemblyService.ComputeConfidence(citations, "Clear answer.");

        result.Should().NotBeNull();
        // Average 0.625 - 0.1 (no high score) = 0.525
        result!.Value.Should().BeLessThan(0.7);
    }

    [Fact]
    public void ComputeConfidence_WithHedgeWords_AppliesPenalty()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc1", 1, 0.90f, "Good chunk")
        };

        var result = RagPromptAssemblyService.ComputeConfidence(citations, "I think maybe this is the rule.");

        result.Should().NotBeNull();
        // 0.90 - 0.1 (hedge words) = 0.80
        result!.Value.Should().BeLessThan(0.9);
    }

    [Fact]
    public void ComputeConfidence_NeverExceedsBounds()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc1", 1, 0.99f, "Perfect match")
        };

        var result = RagPromptAssemblyService.ComputeConfidence(citations, "Definitive answer.");

        result.Should().NotBeNull();
        result!.Value.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void ComputeConfidence_WithBothPenalties_ClampsToZero()
    {
        var citations = new List<ChunkCitation>
        {
            new("doc1", 1, 0.15f, "Very low relevance")
        };

        var result = RagPromptAssemblyService.ComputeConfidence(citations, "I'm not sure, maybe this is right.");

        result.Should().NotBeNull();
        // 0.15 - 0.1 - 0.1 = -0.05, clamped to 0.0
        result!.Value.Should().Be(0.0);
    }

    #endregion

    #region AssemblePromptAsync - Query Expansion

    [Fact]
    public async Task AssemblePrompt_WithQueryExpansion_SearchesMultipleQueries()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Pawn movement rules."));
        SetupRerankerPassthrough();

        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "[\"pawn movement rules\", \"how do pawns advance\"]"
            });

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert - embedding called for original + 2 expansions = 3 times
        _embeddingMock.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        // After Qdrant removal, FTS-only RRF scores are below threshold — no citations
        result.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task AssemblePrompt_QueryExpansionFails_FallsBackToOriginal()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();

        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM unavailable"));

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, CancellationToken.None);

        // Assert - should still work with original query only
        _embeddingMock.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // After Qdrant removal, FTS-only RRF scores are below threshold — no citations
        result.Citations.Should().BeEmpty();
    }

    #endregion

    #region AssemblePromptAsync - Deduplication

    [Fact]
    public async Task AssemblePrompt_DuplicateChunks_DeduplicatedAfterRrfFusion()
    {
        // Arrange — After Qdrant removal, FTS results go through RRF-only scoring.
        // Duplicate FTS entries for the same PdfId+ChunkIndex cause double-counting in RRF,
        // which can boost the normalized score above the 0.55 MinScore threshold.
        // After deduplication, only unique PdfId+ChunkIndex combinations remain.
        SetupSuccessfulEmbedding();

        var doc1Id = Guid.NewGuid();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>
            {
                new(doc1Id, "Same chunk text", 0, 1, 0.90f),
                new(doc1Id, "Different chunk", 1, 1, 0.80f),
                new(doc1Id, "Same chunk text", 0, 1, 0.85f) // duplicate — boosts RRF score above threshold
            });
        SetupRerankerPassthrough();

        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "[\"alternative phrasing\"]"
            });

        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "Question",
            TestGameId, null, null, CancellationToken.None);

        // Assert — Duplicate RRF entries boost doc1:0 score above threshold (~0.97),
        // while doc1:1 with single entry stays below threshold (~0.48).
        // After dedup, only doc1:0 passes the filter.
        var doc1String = doc1Id.ToString();
        result.Citations.Count(c => c.DocumentId == doc1String).Should().Be(1); // only doc1:0 above threshold
    }

    #endregion

    #region AssemblePromptAsync - Null Argument Validation

    [Fact]
    public async Task AssemblePrompt_NullAgentTypology_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = () => service.AssemblePromptAsync(
            null!, "Chess", null, "Question", TestGameId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("agentTypology");
    }

    [Fact]
    public async Task AssemblePrompt_NullGameTitle_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = () => service.AssemblePromptAsync(
            "tutor", null!, null, "Question", TestGameId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("gameTitle");
    }

    [Fact]
    public async Task AssemblePrompt_NullUserQuestion_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = () => service.AssemblePromptAsync(
            "tutor", "Chess", null, null!, TestGameId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("userQuestion");
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullEmbeddingService_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            null!, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("embeddingService");
    }

    [Fact]
    public void Constructor_NullReranker_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, null!, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("reranker");
    }

    [Fact]
    public void Constructor_NullLlmService_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, null!, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("llmService");
    }

    [Fact]
    public void Constructor_NullTextSearchService_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, null!, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("textSearch");
    }

    [Fact]
    public void Constructor_NullExpansionResolver_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, null!,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("expansionResolver");
    }

    [Fact]
    public void Constructor_NullRagEnhancementService_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            null!, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("ragEnhancementService");
    }

    [Fact]
    public void Constructor_NullComplexityClassifier_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, null!, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("complexityClassifier");
    }

    [Fact]
    public void Constructor_NullRelevanceEvaluator_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, null!, _queryExpanderMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("relevanceEvaluator");
    }

    [Fact]
    public void Constructor_NullQueryExpander_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("queryExpander");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new RagPromptAssemblyService(
            _embeddingMock.Object, _rerankerMock.Object, _llmMock.Object, _textSearchMock.Object, _expansionResolverMock.Object,
            _ragEnhancementMock.Object, _complexityClassifierMock.Object, _relevanceEvaluatorMock.Object, _queryExpanderMock.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region Token Estimation

    [Fact]
    public async Task AssemblePrompt_EstimatesTokenCount()
    {
        // Arrange
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Some rule text that is several words long."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "A question about rules",
            TestGameId, null, null, CancellationToken.None);

        // Assert - should have a reasonable token estimate (roughly length/4)
        result.EstimatedTokens.Should().BeGreaterThan(0);
        var totalLength = result.SystemPrompt.Length + result.UserPrompt.Length;
        var expectedMinTokens = (int)Math.Ceiling(totalLength / 4.0) - 1; // allow small rounding
        result.EstimatedTokens.Should().BeGreaterThanOrEqualTo(expectedMinTokens - 1);
    }

    #endregion

    #region Issue #5588 - Expansion Boost

    [Fact]
    public void ApplyExpansionBoost_MultipliesScoresByBoostFactor()
    {
        // Arrange
        var chunks = new List<SearchResultItem>
        {
            CreateChunk("doc1", 0, 0.80f, "Expansion rule A"),
            CreateChunk("doc2", 0, 0.60f, "Expansion rule B")
        };

        // Act
        var boosted = RagPromptAssemblyService.ApplyExpansionBoost(chunks);

        // Assert
        boosted.Should().HaveCount(2);
        boosted[0].Score.Should().BeApproximately(0.80f * 1.3f, 0.001f);
        boosted[1].Score.Should().BeApproximately(0.60f * 1.3f, 0.001f);
    }

    [Fact]
    public void ApplyExpansionBoost_ReSortsByBoostedScore()
    {
        // Arrange — doc2 has lower raw score but after boost would rank differently
        var chunks = new List<SearchResultItem>
        {
            CreateChunk("doc1", 0, 0.60f, "Lower score"),
            CreateChunk("doc2", 0, 0.80f, "Higher score")
        };

        // Act
        var boosted = RagPromptAssemblyService.ApplyExpansionBoost(chunks);

        // Assert — should be sorted by descending boosted score
        boosted[0].Score.Should().BeGreaterThan(boosted[1].Score);
        boosted[0].PdfId.Should().Be("doc2"); // 0.80 * 1.3 = 1.04
        boosted[1].PdfId.Should().Be("doc1"); // 0.60 * 1.3 = 0.78
    }

    [Fact]
    public void ApplyExpansionBoost_BaseGameDocsKeepOriginalScore()
    {
        // Arrange — base game docs are NOT boosted (boost is applied only to expansion results)
        var baseChunks = new List<SearchResultItem>
        {
            CreateChunk("base-doc1", 0, 0.85f, "Base game rule")
        };

        // Act — apply boost factor of 1.0 (no boost)
        var result = RagPromptAssemblyService.ApplyExpansionBoost(baseChunks, 1.0f);

        // Assert
        result[0].Score.Should().BeApproximately(0.85f, 0.001f);
    }

    [Fact]
    public void ExpansionBoostFactor_Is1Point3()
    {
        RagPromptAssemblyService.ExpansionBoostFactor.Should().Be(1.3f);
    }

    [Fact]
    public async Task AssemblePrompt_WithExpansions_SearchesBaseGame()
    {
        // Arrange — After Qdrant removal, the hybrid search still calls FTS for the base game.
        // Expansion game FTS is no longer triggered because the expansion search was
        // part of the vector search loop which has been removed.
        var expansionGameId = Guid.NewGuid();
        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { expansionGameId });

        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Catan", null, "How do cities work?",
            TestGameId, null, null, CancellationToken.None);

        // Assert — FTS is called for the base game ID
        _textSearchMock.Verify(t => t.FullTextSearchAsync(
            TestGameId, It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AssemblePrompt_WithExpansions_IncludesExpansionPriorityInPrompt()
    {
        // Arrange
        var expansionGameId = Guid.NewGuid();
        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { expansionGameId });

        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "arbitro", "Catan", null, "Is this legal?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().Contain("Expansion Priority");
        result.SystemPrompt.Should().Contain("l'espansione prevale sempre");
        result.SystemPrompt.Should().Contain("segnalalo esplicitamente nella risposta");
    }

    [Fact]
    public async Task AssemblePrompt_WithoutExpansions_OmitsExpansionPriority()
    {
        // Arrange — no expansions (default mock)
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "arbitro", "Catan", null, "Is this legal?",
            TestGameId, null, null, CancellationToken.None);

        // Assert
        result.SystemPrompt.Should().NotContain("Expansion Priority");
        result.SystemPrompt.Should().NotContain("l'espansione prevale sempre");
    }

    [Fact]
    public async Task AssemblePrompt_ExpansionResolverFails_ContinuesWithEmptyResults()
    {
        // Arrange — resolver returns empty (catches exceptions internally)
        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        SetupSuccessfulEmbedding();
        SetupTextSearchResults(CreateChunk("doc1", 0, 0.90f, "Rule text."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Catan", null, "Question?",
            TestGameId, null, null, CancellationToken.None);

        // Assert — After Qdrant removal, FTS-only RRF scores are below threshold
        result.Citations.Should().BeEmpty();
        result.SystemPrompt.Should().NotContain("Expansion Priority");
    }

    #endregion
}
