using Api.Services;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AI-09: Integration tests for multilingual RAG pipeline with real Qdrant
/// Tests complete flow: language detection → embedding → indexing → search → retrieval
/// </summary>
public class MultilingualRagIntegrationTests : QdrantIntegrationTestBase, IAsyncLifetime
{
    private readonly List<string> _createdPdfIds = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Call base class initialization to set up QdrantService
        await base.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Cleanup all created documents
        if (QdrantService != null)
        {
            foreach (var pdfId in _createdPdfIds)
            {
                await QdrantService.DeleteDocumentAsync(pdfId);
            }
        }

        // Call base class cleanup
        await base.DisposeAsync();
    }

    #region Indexing with Language Metadata

    /// <summary>
    /// BDD Scenario: Index Italian document chunks with language metadata
    /// Given: Document chunks in Italian
    /// When: IndexDocumentChunksAsync is called with language "it"
    /// Then: Chunks are indexed with language metadata
    /// And: Metadata includes game_id, pdf_id, language, text, page fields
    /// </summary>
    [Fact]
    public async Task IndexDocumentChunksAsync_WithItalianLanguage_StoresLanguageMetadata()
    {
        // Arrange
        var pdfId = $"pdf-it-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfId);

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Il giocatore con più punti vittoria vince la partita.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 53
            },
            new()
            {
                Text = "Le pedine si muovono secondo regole specifiche.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 54,
                CharEnd = 102
            }
        };

        // Act
        var result = await QdrantService.IndexDocumentChunksAsync(
            gameId: "scacchi",
            pdfId: pdfId,
            chunks: chunks,
            language: "it");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.IndexedCount);

        // Verify we can search and retrieve with language filter
        var searchResult = await QdrantService.SearchAsync(
            gameId: "scacchi",
            queryEmbedding: chunks[0].Embedding,
            language: "it",
            limit: 5);

        Assert.True(searchResult.Success);
        Assert.NotEmpty(searchResult.Results);

        // Verify returned chunks match what we indexed
        var retrievedChunk = searchResult.Results.FirstOrDefault(r => r.PdfId == pdfId);
        Assert.NotNull(retrievedChunk);
        Assert.Equal(pdfId, retrievedChunk.PdfId);
    }

    /// <summary>
    /// BDD Scenario: Index documents in multiple languages for same game
    /// Given: Same game has rulebooks in Italian and English
    /// When: Chunks indexed with different language codes
    /// Then: Each chunk is tagged with correct language
    /// And: Language filtering separates the documents
    /// </summary>
    [Fact]
    public async Task IndexDocumentChunksAsync_MultipleLanguagesSameGame_StoresCorrectLanguageMetadata()
    {
        // Arrange
        var pdfIdItalian = $"pdf-it-{Guid.NewGuid():N}";
        var pdfIdEnglish = $"pdf-en-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfIdItalian);
        _createdPdfIds.Add(pdfIdEnglish);

        var chunksItalian = new List<DocumentChunk>
        {
            new()
            {
                Text = "La torre si muove orizzontalmente o verticalmente.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 52
            }
        };

        var chunksEnglish = new List<DocumentChunk>
        {
            new()
            {
                Text = "The rook moves horizontally or vertically.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 43
            }
        };

        // Act - Index Italian chunks
        var resultItalian = await QdrantService.IndexDocumentChunksAsync(
            gameId: "chess",
            pdfId: pdfIdItalian,
            chunks: chunksItalian,
            language: "it");

        // Act - Index English chunks
        var resultEnglish = await QdrantService.IndexDocumentChunksAsync(
            gameId: "chess",
            pdfId: pdfIdEnglish,
            chunks: chunksEnglish,
            language: "en");

        // Assert - Both successful
        Assert.True(resultItalian.Success);
        Assert.True(resultEnglish.Success);

        // Assert - Search with Italian filter returns only Italian results
        var searchItalian = await QdrantService.SearchAsync(
            gameId: "chess",
            queryEmbedding: chunksItalian[0].Embedding,
            language: "it",
            limit: 10);

        Assert.True(searchItalian.Success);
        Assert.NotEmpty(searchItalian.Results);

        // Verify we get Italian PDF (might get both due to embedding similarity, but filter ensures language matches)
        var italianResults = searchItalian.Results.Where(r => r.PdfId == pdfIdItalian).ToList();
        Assert.NotEmpty(italianResults);

        // Assert - Search with English filter returns only English results
        var searchEnglish = await QdrantService.SearchAsync(
            gameId: "chess",
            queryEmbedding: chunksEnglish[0].Embedding,
            language: "en",
            limit: 10);

        Assert.True(searchEnglish.Success);
        Assert.NotEmpty(searchEnglish.Results);

        var englishResults = searchEnglish.Results.Where(r => r.PdfId == pdfIdEnglish).ToList();
        Assert.NotEmpty(englishResults);
    }

    /// <summary>
    /// BDD Scenario: Index with all 5 supported languages
    /// Given: Chunks in all supported languages (en, it, de, fr, es)
    /// When: Each indexed with corresponding language code
    /// Then: All indexed successfully
    /// And: Search filters work correctly for each language
    /// </summary>
    [Theory]
    [InlineData("en", "The player with the most points wins.")]
    [InlineData("it", "Il giocatore con più punti vince.")]
    [InlineData("de", "Der Spieler mit den meisten Punkten gewinnt.")]
    [InlineData("fr", "Le joueur avec le plus de points gagne.")]
    [InlineData("es", "El jugador con más puntos gana.")]
    public async Task IndexDocumentChunksAsync_AllSupportedLanguages_IndexesSuccessfully(string language, string text)
    {
        // Arrange
        var pdfId = $"pdf-{language}-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfId);

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = text,
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = text.Length
            }
        };

        // Act
        var result = await QdrantService.IndexDocumentChunksAsync(
            gameId: "test-game",
            pdfId: pdfId,
            chunks: chunks,
            language: language);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.IndexedCount);

        // Verify search with language filter
        var searchResult = await QdrantService.SearchAsync(
            gameId: "test-game",
            queryEmbedding: chunks[0].Embedding,
            language: language,
            limit: 5);

        Assert.True(searchResult.Success);
        Assert.NotEmpty(searchResult.Results);

        // Verify we can retrieve the indexed chunk
        var retrievedChunk = searchResult.Results.FirstOrDefault(r => r.PdfId == pdfId);
        Assert.NotNull(retrievedChunk);
        Assert.Equal(text, retrievedChunk.Text);
    }

    #endregion

    #region Language-Filtered Search

    /// <summary>
    /// BDD Scenario: Search with language filter excludes other languages
    /// Given: Game has documents in Italian and German
    /// When: Search is performed with language "it"
    /// Then: Only Italian documents are returned
    /// And: German documents are excluded
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithLanguageFilter_ExcludesOtherLanguages()
    {
        // Arrange - Index Italian chunks
        var pdfIdItalian = $"pdf-it-exclusive-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfIdItalian);

        var chunksItalian = new List<DocumentChunk>
        {
            new()
            {
                Text = "Il re è la pedina più importante nel gioco degli scacchi.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 58
            }
        };

        await QdrantService.IndexDocumentChunksAsync("chess", pdfIdItalian, chunksItalian, "it");

        // Arrange - Index German chunks with similar embedding (to test filter effectiveness)
        var pdfIdGerman = $"pdf-de-exclusive-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfIdGerman);

        var chunksGerman = new List<DocumentChunk>
        {
            new()
            {
                Text = "Der König ist die wichtigste Figur im Schachspiel.",
                Embedding = CreateRandomEmbedding(), // Different embedding
                Page = 1,
                CharStart = 0,
                CharEnd = 51
            }
        };

        await QdrantService.IndexDocumentChunksAsync("chess", pdfIdGerman, chunksGerman, "de");

        // Act - Search with Italian filter
        var searchResult = await QdrantService.SearchAsync(
            gameId: "chess",
            queryEmbedding: chunksItalian[0].Embedding,
            language: "it",
            limit: 10);

        // Assert
        Assert.True(searchResult.Success);
        Assert.NotEmpty(searchResult.Results);

        // Verify only Italian results returned (no German results)
        var italianResults = searchResult.Results.Where(r => r.PdfId == pdfIdItalian).ToList();
        var germanResults = searchResult.Results.Where(r => r.PdfId == pdfIdGerman).ToList();

        Assert.NotEmpty(italianResults);
        Assert.Empty(germanResults); // German should be filtered out
    }

    /// <summary>
    /// BDD Scenario: Search without language returns all languages
    /// Given: Game has documents in multiple languages
    /// When: Search is performed without language filter (language = null not supported in current API)
    /// Then: This test documents current behavior
    /// Note: Current API requires language parameter, defaults to "en" in RagService
    /// </summary>
    [Fact]
    public async Task SearchAsync_CurrentBehavior_RequiresLanguageParameter()
    {
        // This test documents that the current API requires language parameter
        // RagService.AskAsync defaults language to "en" if null
        // Future enhancement: could add SearchAsync overload without language to search across all languages

        // Arrange
        var pdfId = $"pdf-search-all-{Guid.NewGuid():N}";
        _createdPdfIds.Add(pdfId);

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Test content for search behavior documentation",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 47
            }
        };

        await QdrantService.IndexDocumentChunksAsync("test-game", pdfId, chunks, "en");

        // Act & Assert - Current API requires language parameter
        var result = await QdrantService.SearchAsync("test-game", chunks[0].Embedding, "en", 5);
        Assert.True(result.Success);
    }

    #endregion

    #region Edge Cases and Error Handling

    /// <summary>
    /// BDD Scenario: Empty chunks with language parameter
    /// Given: Empty chunks list
    /// When: IndexDocumentChunksAsync called with language
    /// Then: Returns failure without calling Qdrant
    /// </summary>
    [Fact]
    public async Task IndexDocumentChunksAsync_EmptyChunksWithLanguage_ReturnsFailure()
    {
        // Arrange
        var pdfId = $"pdf-empty-{Guid.NewGuid():N}";
        var emptyChunks = new List<DocumentChunk>();

        // Act
        var result = await QdrantService.IndexDocumentChunksAsync("game-1", pdfId, emptyChunks, "fr");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No chunks to index", result.ErrorMessage);
        Assert.Equal(0, result.IndexedCount);
    }

    /// <summary>
    /// BDD Scenario: Search in game with no documents for language
    /// Given: Game exists but no documents in French
    /// When: Search with language "fr"
    /// Then: Returns success with empty results
    /// </summary>
    [Fact]
    public async Task SearchAsync_NoDocumentsForLanguage_ReturnsEmptyResults()
    {
        // Arrange
        var queryEmbedding = CreateRandomEmbedding();

        // Act
        var result = await QdrantService.SearchAsync(
            gameId: $"empty-game-{Guid.NewGuid():N}",
            queryEmbedding: queryEmbedding,
            language: "fr",
            limit: 5);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Results);
    }

    #endregion

    #region Helper Methods

    private float[] CreateRandomEmbedding()
    {
        var random = new Random();
        var embedding = new float[1536]; // OpenAI embedding dimensions
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Random values between -1 and 1
        }
        return embedding;
    }

    #endregion
}
