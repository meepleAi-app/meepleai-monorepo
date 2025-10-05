using Api.Services;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Integration tests for QdrantService using real Qdrant container
/// </summary>
public class QdrantServiceIntegrationTests : QdrantIntegrationTestBase
{
    [Fact]
    public async Task EnsureCollectionExistsAsync_WhenCollectionDoesNotExist_CreatesCollection()
    {
        // Collection is already created in InitializeAsync, so this tests idempotency

        // Act
        await QdrantService.EnsureCollectionExistsAsync();

        // Assert - no exception thrown
        Assert.NotNull(QdrantService);
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_WithValidChunks_IndexesSuccessfully()
    {
        // Arrange
        var embedding1 = CreateRandomEmbedding();
        var embedding2 = CreateRandomEmbedding();

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "This is the first test chunk about game rules.",
                Embedding = embedding1,
                Page = 1,
                CharStart = 0,
                CharEnd = 48
            },
            new()
            {
                Text = "This is the second test chunk about scoring.",
                Embedding = embedding2,
                Page = 1,
                CharStart = 49,
                CharEnd = 94
            }
        };

        // Act
        var result = await QdrantService.IndexDocumentChunksAsync(
            gameId: "test-game",
            pdfId: "test-pdf-1",
            chunks: chunks);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.IndexedCount);
    }

    [Fact]
    public async Task SearchAsync_AfterIndexing_ReturnsRelevantResults()
    {
        // Arrange - Index some chunks first
        var embedding = CreateRandomEmbedding();
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "The player with the most victory points wins the game.",
                Embedding = embedding,
                Page = 5,
                CharStart = 100,
                CharEnd = 155
            }
        };

        await QdrantService.IndexDocumentChunksAsync(
            gameId: "game-search",
            pdfId: "pdf-search-1",
            chunks: chunks);

        // Act - Search with similar embedding
        var searchResult = await QdrantService.SearchAsync(
            gameId: "game-search",
            queryEmbedding: embedding,
            limit: 5);

        // Assert
        Assert.True(searchResult.Success);
        Assert.Null(searchResult.ErrorMessage);
        Assert.NotEmpty(searchResult.Results);
        Assert.Contains(searchResult.Results, r => r.Text.Contains("victory points"));
        Assert.Equal(5, searchResult.Results[0].Page);
        Assert.Equal("pdf-search-1", searchResult.Results[0].PdfId);
    }

    [Fact]
    public async Task SearchAsync_WithoutTenantFilter_ReturnsResults()
    {
        // Arrange - Index chunks for one game (tenancy is global)
        var embedding = CreateRandomEmbedding();
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Tenant isolation test chunk.",
                Embedding = embedding,
                Page = 1,
                CharStart = 0,
                CharEnd = 28
            }
        };

        await QdrantService.IndexDocumentChunksAsync(
            gameId: "game-1",
            pdfId: "pdf-tenant-test",
            chunks: chunks);

        // Act - Search
        var searchResult = await QdrantService.SearchAsync(
            gameId: "game-1",
            queryEmbedding: embedding,
            limit: 5);

        // Assert - Results are returned regardless of tenant context
        Assert.True(searchResult.Success);
        Assert.NotEmpty(searchResult.Results);
    }

    [Fact]
    public async Task SearchAsync_WithDifferentGame_ReturnsNoResults()
    {
        // Arrange - Index chunks for one game
        var embedding = CreateRandomEmbedding();
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Game isolation test chunk.",
                Embedding = embedding,
                Page = 1,
                CharStart = 0,
                CharEnd = 26
            }
        };

        await QdrantService.IndexDocumentChunksAsync(
            gameId: "chess",
            pdfId: "pdf-game-test",
            chunks: chunks);

        // Act - Search with different game
        var searchResult = await QdrantService.SearchAsync(
            gameId: "monopoly",
            queryEmbedding: embedding,
            limit: 5);

        // Assert - Should find no results due to game isolation
        Assert.True(searchResult.Success);
        Assert.Empty(searchResult.Results);
    }

    [Fact]
    public async Task DeleteDocumentAsync_AfterIndexing_RemovesDocument()
    {
        // Arrange - Index chunks
        var embedding = CreateRandomEmbedding();
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "This chunk will be deleted.",
                Embedding = embedding,
                Page = 1,
                CharStart = 0,
                CharEnd = 27
            }
        };

        const string pdfId = "pdf-to-delete";
        await QdrantService.IndexDocumentChunksAsync(
            gameId: "game-delete",
            pdfId: pdfId,
            chunks: chunks);

        // Verify it was indexed
        var searchBefore = await QdrantService.SearchAsync(
            gameId: "game-delete",
            queryEmbedding: embedding,
            limit: 5);
        Assert.NotEmpty(searchBefore.Results);

        // Act - Delete the document
        var deleteResult = await QdrantService.DeleteDocumentAsync(pdfId);

        // Assert
        Assert.True(deleteResult);

        // Verify it was deleted
        var searchAfter = await QdrantService.SearchAsync(
            gameId: "game-delete",
            queryEmbedding: embedding,
            limit: 5);
        Assert.Empty(searchAfter.Results);
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_WithMultiplePages_IndexesCorrectly()
    {
        // Arrange
        var chunks = new List<DocumentChunk>();
        for (int page = 1; page <= 3; page++)
        {
            for (int chunk = 0; chunk < 2; chunk++)
            {
                chunks.Add(new DocumentChunk
                {
                    Text = $"Page {page}, chunk {chunk} content",
                    Embedding = CreateRandomEmbedding(),
                    Page = page,
                    CharStart = chunk * 50,
                    CharEnd = (chunk + 1) * 50
                });
            }
        }

        // Act
        var result = await QdrantService.IndexDocumentChunksAsync(
            gameId: "game-multipage",
            pdfId: "pdf-multipage",
            chunks: chunks);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(6, result.IndexedCount);

        // Verify we can search and get results from different pages
        var searchResult = await QdrantService.SearchAsync(
            gameId: "game-multipage",
            queryEmbedding: chunks[0].Embedding,
            limit: 10);

        Assert.True(searchResult.Success);
        Assert.NotEmpty(searchResult.Results);
    }

    [Fact]
    public async Task SearchAsync_WithLimitParameter_RespectsLimit()
    {
        // Arrange - Index 10 chunks
        var chunks = new List<DocumentChunk>();
        var baseEmbedding = CreateRandomEmbedding();

        for (int i = 0; i < 10; i++)
        {
            chunks.Add(new DocumentChunk
            {
                Text = $"Search limit test chunk {i}",
                Embedding = baseEmbedding,
                Page = 1,
                CharStart = i * 30,
                CharEnd = (i + 1) * 30
            });
        }

        await QdrantService.IndexDocumentChunksAsync(
            gameId: "game-limit",
            pdfId: "pdf-limit",
            chunks: chunks);

        // Act - Search with limit of 3
        var searchResult = await QdrantService.SearchAsync(
            gameId: "game-limit",
            queryEmbedding: baseEmbedding,
            limit: 3);

        // Assert
        Assert.True(searchResult.Success);
        Assert.Equal(3, searchResult.Results.Count);
    }

    /// <summary>
    /// Creates a random embedding vector of size 1536 (matching text-embedding-3-small)
    /// </summary>
    private static float[] CreateRandomEmbedding()
    {
        var random = new Random();
        var embedding = new float[1536];

        // Generate normalized random vector
        float sum = 0;
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
            sum += embedding[i] * embedding[i];
        }

        // Normalize
        float magnitude = (float)Math.Sqrt(sum);
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
