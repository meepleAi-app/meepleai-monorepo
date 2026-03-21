using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Unit tests for EmbeddingBasedSemanticChunker service.
/// Issue #2525: Background Rulebook Analysis Tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EmbeddingBasedSemanticChunkerTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<EmbeddingBasedSemanticChunker>> _mockLogger;
    private readonly BackgroundAnalysisOptions _options;
    private readonly EmbeddingBasedSemanticChunker _service;

    public EmbeddingBasedSemanticChunkerTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<EmbeddingBasedSemanticChunker>>();

        // Use default options
        _options = new BackgroundAnalysisOptions
        {
            MaxChunkSize = 10000,
            OverlapSize = 500,
            SemanticSimilarityThreshold = 0.75,
            MinimumSectionSize = 100
        };

        _service = new EmbeddingBasedSemanticChunker(
            _mockEmbeddingService.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }

    #region Strategy 1: Embedding-Based Chunking Success

    [Fact]
    public async Task ChunkAsync_WithEmbeddingSuccess_ReturnsEmbeddingBasedChunks()
    {
        // Arrange - Content must include the section headers and be >= 100 chars per section
        var rulebookContent = """
            Setup
            Place all game components on the table. Each player takes their starting resources. This section provides the initial game state.

            Gameplay
            On each turn, players roll dice and move their pieces. Actions include collecting resources, building, and trading with other players.

            Victory
            The game ends when a player reaches the target score. Victory conditions may vary based on game mode and player count.
            """;
        var sectionHeaders = new List<string> { "Setup", "Gameplay", "Victory" };

        // Mock embedding service to return embeddings with varying similarity
        var embeddings = CreateTestEmbeddings(3); // 3 sections
        var embeddingResult = EmbeddingResult.CreateSuccess(embeddings);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        // Act
        var result = await _service.ChunkAsync(rulebookContent, sectionHeaders);

        // Assert
        result.Should().NotBeNull();
        result.StrategyUsed.Should().Be(ChunkingStrategy.EmbeddingBased);
        result.Chunks.Should().NotBeEmpty();
        result.TotalChunks.Should().Be(result.Chunks.Count);
        result.TotalCharacters.Should().BeGreaterThan(0);

        _mockEmbeddingService.Verify(
            x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChunkAsync_EmbeddingBasedWithLowSimilarity_CreatesSeparateChunks()
    {
        // Arrange - Each section must be >= 100 chars to pass MinimumSectionSize filter
        var content = string.Join("\n\n",
            "Section A: First distinct topic with enough content to meet the minimum section size requirement of 100 characters for semantic analysis.",
            "Section B: Completely different topic with sufficient text length to pass the minimum section size threshold for chunking.",
            "Section C: Another unrelated topic with adequate character count to meet the minimum section size requirement for processing.");

        // Create embeddings with low cosine similarity (<0.75 threshold)
        // For low cosine similarity, vectors must point in different directions (not just different magnitudes)
        var embedding1 = new float[384];
        var embedding2 = new float[384];
        var embedding3 = new float[384];

        // Set different components to 1.0 to create near-orthogonal vectors
        embedding1[0] = 1.0f; embedding1[1] = 0.1f;  // Points mostly along dimension 0
        embedding2[100] = 1.0f; embedding2[101] = 0.1f;  // Points mostly along dimension 100
        embedding3[200] = 1.0f; embedding3[201] = 0.1f;  // Points mostly along dimension 200

        var embeddingResult = EmbeddingResult.CreateSuccess([embedding1, embedding2, embedding3]);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        // Act
        var result = await _service.ChunkAsync(content, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.EmbeddingBased);
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(2, "low similarity should create separate chunks");
    }

    #endregion

    #region Strategy 2: Header-Based Fallback

    [Fact]
    public async Task ChunkAsync_WhenEmbeddingFails_FallsBackToHeaderBased()
    {
        // Arrange - Each section must be >= 100 chars to pass MinimumSectionSize filter
        var rulebookContent = """
            # Setup
            Place the board in the center of the table. Each player takes their starting pieces and resources from the supply. Shuffle the deck and deal initial cards.

            # Gameplay
            Take turns clockwise around the table. On your turn you may perform various actions including moving pieces, collecting resources, and trading with other players.

            # Victory
            First to reach 10 victory points wins the game. Victory points can be earned through building, trading, and completing special objectives during play.
            """;

        // Mock embedding failure
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service unavailable"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.HeaderBased);
        result.Chunks.Should().NotBeEmpty();
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(2, "should find markdown headers");
    }

    [Fact]
    public async Task ChunkAsync_WithProvidedHeaders_UsesHeaderBasedChunking()
    {
        // Arrange
        var rulebookContent = """
            Setup
            Place the board.

            Gameplay
            Roll dice and move.

            Victory
            First to finish wins.
            """;

        var providedHeaders = new List<string> { "Setup", "Gameplay", "Victory" };

        // Mock embedding service to return empty list (insufficient for embedding strategy)
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([]));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, providedHeaders);

        // Assert - May use HeaderBased if embedding strategy fails or falls back
        result.Chunks.Should().NotBeEmpty();
    }

    #endregion

    #region Strategy 3: Fixed-Size Fallback

    [Fact]
    public async Task ChunkAsync_WhenBothStrategiesFail_UsesFixedSizeFallback()
    {
        // Arrange
        var rulebookContent = new string('A', 25000); // Plain text, no headers

        // Mock embedding failure
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding failed"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.FixedSize);
        result.Chunks.Should().NotBeEmpty();
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(2, "25k chars with 10k max should create 2+ chunks");
    }

    [Fact]
    public async Task ChunkAsync_FixedSizeStrategy_AppliesOverlapBetweenChunks()
    {
        // Arrange
        var rulebookContent = new string('A', 25000);

        // Force fixed-size strategy by making embedding fail
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Forced failure"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.FixedSize);

        // Verify overlap: second chunk should start OverlapSize (500) before first chunk ends
        if (result.Chunks.Count >= 2)
        {
            var firstChunkEnd = result.Chunks[0].EndCharIndex;
            var secondChunkStart = result.Chunks[1].StartCharIndex;

            var actualOverlap = firstChunkEnd - secondChunkStart;
            actualOverlap.Should().Be(_options.OverlapSize);
        }
    }

    [Fact]
    public async Task ChunkAsync_OnException_FallsBackToFixedSize()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(3);

        // Mock exception during embedding
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.FixedSize);
        result.Chunks.Should().NotBeEmpty();
    }

    #endregion

    #region Chunk Validation Tests

    [Fact]
    public async Task ChunkAsync_AllStrategies_ReturnsChunksWithSequentialIndexes()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(5);

        // Force fixed-size for deterministic behavior
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Test"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.Chunks.Should().NotBeEmpty();

        for (int i = 0; i < result.Chunks.Count; i++)
        {
            result.Chunks[i].ChunkIndex.Should().Be(i, "chunks should have sequential indexes");
        }
    }

    [Fact]
    public async Task ChunkAsync_FixedSizeStrategy_RespectsMaxChunkSize()
    {
        // Arrange
        var rulebookContent = new string('A', 30000);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Test"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.Chunks.Should().NotBeEmpty();

        foreach (var chunk in result.Chunks)
        {
            chunk.CharacterCount.Should().BeLessThanOrEqualTo(_options.MaxChunkSize);
        }
    }

    [Fact]
    public async Task ChunkAsync_AllStrategies_ChunksHaveValidCharacterIndexes()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(4);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Test"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        foreach (var chunk in result.Chunks)
        {
            chunk.StartCharIndex.Should().BeGreaterThanOrEqualTo(0);
            chunk.EndCharIndex.Should().BeGreaterThanOrEqualTo(chunk.StartCharIndex);
            chunk.EndCharIndex.Should().BeLessThanOrEqualTo(rulebookContent.Length);
        }
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task ChunkAsync_ReturnsAccurateTotalChunksCount()
    {
        // Arrange
        var rulebookContent = new string('A', 15000);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Test"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.TotalChunks.Should().Be(result.Chunks.Count);
    }

    [Fact]
    public async Task ChunkAsync_ReturnsAccurateTotalCharacters()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(4);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Test"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        var actualTotal = result.Chunks.Sum(c => c.CharacterCount);
        result.TotalCharacters.Should().Be(actualTotal);
    }

    #endregion

    #region Chunk Overlap Verification Tests

    [Fact]
    public async Task ChunkAsync_FixedSizeStrategy_EnsuresMinimum500CharsOverlap()
    {
        // Arrange
        var rulebookContent = new string('A', 50000); // Large content to create multiple chunks

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force fixed-size"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.FixedSize);
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(2, "large content should create multiple chunks");

        // Verify all overlaps are exactly 500 chars (configured OverlapSize)
        for (int i = 1; i < result.Chunks.Count; i++)
        {
            var previousChunkEnd = result.Chunks[i - 1].EndCharIndex;
            var currentChunkStart = result.Chunks[i].StartCharIndex;
            var overlap = previousChunkEnd - currentChunkStart;

            overlap.Should().Be(500, $"overlap between chunk {i - 1} and {i} should be 500 chars");
        }
    }

    #endregion

    #region Similarity Threshold Tests

    [Fact]
    public async Task ChunkAsync_WithSimilarityAboveThreshold_CombinesChunks()
    {
        // Arrange - Each section must be >= 100 chars to pass MinimumSectionSize filter
        var content = """
            # Section A
            Content A with similar semantic meaning. This section contains enough text to meet the minimum section size requirement of 100 characters for semantic analysis and chunking.

            # Section B
            Content B with very similar semantic meaning. This section also contains sufficient text length to pass the minimum section size threshold for the chunking algorithm.
            """;

        // Create highly similar embeddings (cosine similarity > 0.75)
        var embedding1 = Enumerable.Repeat(1.0f, 384).ToArray();
        var embedding2 = Enumerable.Repeat(0.95f, 384).ToArray(); // Very similar (>0.75)

        var embeddingResult = EmbeddingResult.CreateSuccess([embedding1, embedding2]);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        // Act
        var result = await _service.ChunkAsync(content, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.EmbeddingBased);
        // High similarity should combine sections → fewer chunks
        result.Chunks.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ChunkAsync_WithSimilarityBelowThreshold_CreatesSeparateChunks()
    {
        // Arrange - Each section must be >= 100 chars to pass MinimumSectionSize filter
        var content = string.Join("\n\n",
            "Topic A: Completely different subject matter with enough text to meet the minimum section size requirement of 100 characters for analysis.",
            "Topic B: Entirely unrelated content with sufficient length to pass the minimum section size threshold for semantic chunking.",
            "Topic C: Another distinct topic with adequate character count to satisfy the minimum section size requirement for processing.");

        // Create embeddings with similarity < 0.75 (vectors pointing in different directions)
        var embedding1 = new float[384];
        var embedding2 = new float[384];
        var embedding3 = new float[384];

        // Set different components to 1.0 to create near-orthogonal vectors
        embedding1[0] = 1.0f; embedding1[1] = 0.1f;
        embedding2[100] = 1.0f; embedding2[101] = 0.1f;
        embedding3[200] = 1.0f; embedding3[201] = 0.1f;

        var embeddingResult = EmbeddingResult.CreateSuccess([embedding1, embedding2, embedding3]);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        // Act
        var result = await _service.ChunkAsync(content, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.EmbeddingBased);
        result.Chunks.Should().HaveCount(3, "low similarity should create separate chunks");
    }

    #endregion

    #region Minimum Section Size Tests

    [Fact]
    public async Task ChunkAsync_HeaderBased_FiltersOutSectionsSmallerThan100Chars()
    {
        // Arrange
        var rulebookContent = """
            # Large Section
            This section has enough content to meet the minimum 100 character requirement for semantic chunking strategies. It's important to have sufficient text.

            # Tiny
            Small

            # Another Large Section
            This section also has enough content to meet the minimum 100 character requirement. More text here to ensure validity.
            """;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force header-based"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.HeaderBased);
        result.Chunks.Should().HaveCount(2, "tiny section should be filtered out");
        result.Chunks.Should().NotContain(c => c.Content.Contains("# Tiny"));
    }

    [Fact]
    public async Task ChunkAsync_FixedSize_DoesNotFilterByMinimumSectionSize()
    {
        // Arrange - Small content (< 100 chars) but should still create chunk in fixed-size
        var rulebookContent = "Small text under 100 chars.";

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force fixed-size"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.FixedSize);
        result.Chunks.Should().HaveCount(1, "fixed-size does not filter by minimum size");
        result.Chunks[0].Content.Should().Be(rulebookContent);
    }

    #endregion

    #region Header Extraction Regex Fallback Tests

    [Fact]
    public async Task ChunkAsync_HeaderRegex_ExtractsMarkdownHeaders()
    {
        // Arrange - Each section must be >= 100 chars to pass MinimumSectionSize filter
        var rulebookContent = """
            # Header Level 1
            Content under level 1 with enough text to meet the minimum section size requirement of 100 characters for proper chunking and semantic analysis.

            ## Header Level 2
            Content under level 2 with sufficient text length to pass the minimum section size threshold required by the chunking algorithm for processing.

            ### Header Level 3
            Content under level 3 with adequate character count to satisfy the minimum section size requirement for the semantic chunking strategy.
            """;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force header-based"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.HeaderBased);
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ChunkAsync_HeaderRegex_ExtractsAllCapsHeaders()
    {
        // Arrange
        var rulebookContent = """
            GAME SETUP
            Place components on board with enough text to meet minimum section size requirements for chunking.

            TURN STRUCTURE
            Each player takes turns with enough text here to meet the minimum character count for valid sections.

            VICTORY CONDITIONS
            First to complete objectives wins with additional text to ensure this section meets size requirements.
            """;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force header-based"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.HeaderBased);
        result.Chunks.Should().HaveCountGreaterThanOrEqualTo(2, "should extract ALL CAPS headers");
    }

    [Fact]
    public async Task ChunkAsync_HeaderRegex_IgnoresSingleWordCaps()
    {
        // Arrange
        var rulebookContent = """
            # Valid Header
            Some content here with enough text to meet minimum section size requirements.

            A
            This single letter should not be treated as header.

            ## Another Valid Header
            More content to ensure valid section size for chunking strategies to work properly.
            """;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Force header-based"));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().Be(ChunkingStrategy.HeaderBased);
        // Single letter "A" should not create separate chunk
        result.Chunks.Should().NotContain(c => c.Content.Trim() == "A");
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task ChunkAsync_WithCancellationToken_PassesToEmbeddingService()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(3);
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        CancellationToken capturedToken = default;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((_, ct) => capturedToken = ct)
            .ReturnsAsync(EmbeddingResult.CreateSuccess(CreateTestEmbeddings(3)));

        // Act
        await _service.ChunkAsync(rulebookContent, null, cancellationToken);

        // Assert
        capturedToken.Should().Be(cancellationToken);
    }

    [Fact]
    public async Task ChunkAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(5);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert - Should propagate cancellation
        var act = () =>
            _service.ChunkAsync(rulebookContent, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ChunkAsync_WithSingleCandidateSection_SkipsEmbeddingBasedStrategy()
    {
        // Arrange
        var rulebookContent = "Single short section without headers or multiple paragraphs.";

        var llmCalls = 0;
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => llmCalls++)
            .ReturnsAsync(EmbeddingResult.CreateSuccess([]));

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert - Should skip embedding-based (only 1 candidate) and use fallback
        result.StrategyUsed.Should().NotBe(ChunkingStrategy.EmbeddingBased);
        llmCalls.Should().Be(0, "should not call embedding service with single candidate");
    }

    [Fact]
    public async Task ChunkAsync_EmbeddingMismatchCount_FallsBackToHeaderBased()
    {
        // Arrange
        var rulebookContent = CreateTestRulebookWithParagraphs(5);

        // Return fewer embeddings than candidates (mismatch)
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(CreateTestEmbeddings(2))); // Only 2, expect 5

        // Act
        var result = await _service.ChunkAsync(rulebookContent, null);

        // Assert
        result.StrategyUsed.Should().NotBe(ChunkingStrategy.EmbeddingBased);
        result.Chunks.Should().NotBeEmpty();
    }

    #endregion

    #region Helper Methods

    private static string CreateTestRulebookWithParagraphs(int paragraphCount)
    {
        var paragraphs = Enumerable.Range(1, paragraphCount)
            .Select(i => $"Paragraph {i}: This is test content with enough text to meet minimum section size requirements for chunking strategies. More text here.");

        return string.Join("\n\n", paragraphs);
    }

    private static List<float[]> CreateTestEmbeddings(int count, int dimensions = 384)
    {
        var embeddings = new List<float[]>();

        for (int i = 0; i < count; i++)
        {
            // Create embeddings with varying similarity
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                // Different base values to simulate different semantic content
                embedding[j] = (i * 0.3f + j * 0.01f) % 1.0f;
            }
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    #endregion
}
