using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD Unit tests for PdfIndexingService (AI-01)
/// Tests the service layer without HTTP dependencies
/// </summary>
public class PdfIndexingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _db;
    private readonly Mock<ITextChunkingService> _mockChunkingService;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IQdrantService> _mockQdrantService;
    private readonly PdfIndexingService _service;

    public PdfIndexingServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new MeepleAiDbContext(options);
        _db.Database.EnsureCreated();

        _mockChunkingService = new Mock<ITextChunkingService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockQdrantService = new Mock<IQdrantService>();

        _service = new PdfIndexingService(
            _db,
            _mockChunkingService.Object,
            _mockEmbeddingService.Object,
            _mockQdrantService.Object,
            NullLogger<PdfIndexingService>.Instance);
    }

    public void Dispose()
    {
        _db?.Dispose();
        _connection?.Dispose();
    }

    /// <summary>
    /// Scenario: Successfully index a PDF document for semantic search
    /// </summary>
    [Fact]
    public async Task IndexPdfAsync_WithValidExtractedText_IndexesSuccessfully()
    {
        // GIVEN: A PDF with extracted text
        var user = new UserEntity
        {
            Id = "user-1",
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        var game = new GameEntity { Id = "tic-tac-toe", Name = "Tic-Tac-Toe", CreatedAt = DateTime.UtcNow };
        var extractedText = "Players alternate marking X or O. Three in a row wins.";
        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-1",
            GameId = "tic-tac-toe",
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "user-1",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = extractedText,
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow,
            PageCount = 1,
            CharacterCount = extractedText.Length
        };

        _db.Users.Add(user);
        _db.Games.Add(game);
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        // Mock chunking to return 2 chunks
        var textChunks = new List<TextChunk>
        {
            new() { Text = "Players alternate marking X or O.", Index = 0, Page = 1, CharStart = 0, CharEnd = 33 },
            new() { Text = "Three in a row wins.", Index = 1, Page = 1, CharStart = 34, CharEnd = 54 }
        };
        _mockChunkingService.Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(textChunks);

        // Mock embedding generation
        var embeddings = new List<float[]>
        {
            new float[1536], // Embedding for chunk 1
            new float[1536]  // Embedding for chunk 2
        };
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(embeddings));

        // Mock Qdrant indexing
        _mockQdrantService.Setup(x => x.IndexDocumentChunksAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexResult.CreateSuccess(2));

        // WHEN: I trigger indexing for the PDF
        var result = await _service.IndexPdfAsync("pdf-1");

        // THEN: The indexing should succeed
        Assert.True(result.Success);
        Assert.NotNull(result.VectorDocumentId);
        Assert.Equal(2, result.ChunkCount);
        Assert.NotNull(result.IndexedAt);

        // AND: The VectorDocumentEntity should be persisted with status "completed"
        var vectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.Id == result.VectorDocumentId);

        Assert.NotNull(vectorDoc);
        Assert.Equal("completed", vectorDoc.IndexingStatus);
        Assert.Equal("tic-tac-toe", vectorDoc.GameId);
        Assert.Equal("pdf-1", vectorDoc.PdfDocumentId);
        Assert.Equal(2, vectorDoc.ChunkCount);
        Assert.Equal(extractedText.Length, vectorDoc.TotalCharacters);
        Assert.NotNull(vectorDoc.IndexedAt);
        Assert.Null(vectorDoc.IndexingError);
        Assert.Equal("openai/text-embedding-3-small", vectorDoc.EmbeddingModel);
        Assert.Equal(1536, vectorDoc.EmbeddingDimensions);
    }

    /// <summary>
    /// Scenario: Attempt to index a PDF without extracted text
    /// </summary>
    [Fact]
    public async Task IndexPdfAsync_WithoutExtractedText_ReturnsBadRequest()
    {
        // GIVEN: A PDF without extracted text
        var user = new UserEntity
        {
            Id = "user-2",
            Email = "test2@example.com",
            DisplayName = "Test User 2",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        var game = new GameEntity { Id = "chess", Name = "Chess", CreatedAt = DateTime.UtcNow };
        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-2",
            GameId = "chess",
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "user-2",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = null,
            ProcessingStatus = "pending",
            ProcessedAt = null,
            PageCount = 0,
            CharacterCount = 0
        };

        _db.Users.Add(user);
        _db.Games.Add(game);
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        // WHEN: I trigger indexing
        var result = await _service.IndexPdfAsync("pdf-2");

        // THEN: Should return failure with appropriate error code
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.TextExtractionRequired, result.ErrorCode);
        Assert.Contains("text extraction required", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Attempt to index a non-existent PDF
    /// </summary>
    [Fact]
    public async Task IndexPdfAsync_NonExistentPdf_ReturnsNotFound()
    {
        // WHEN: I trigger indexing for a non-existent PDF
        var result = await _service.IndexPdfAsync("non-existent-pdf");

        // THEN: Should return failure with PdfNotFound error code
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.PdfNotFound, result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Re-index an already indexed PDF (idempotency)
    /// </summary>
    [Fact]
    public async Task IndexPdfAsync_AlreadyIndexed_ReindexesSuccessfully()
    {
        // GIVEN: A PDF that has already been indexed
        var user = new UserEntity
        {
            Id = "user-3",
            Email = "test3@example.com",
            DisplayName = "Test User 3",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        var game = new GameEntity { Id = "tic-tac-toe", Name = "Tic-Tac-Toe", CreatedAt = DateTime.UtcNow };
        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-3",
            GameId = "tic-tac-toe",
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "user-3",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = "Test content",
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow,
            PageCount = 1,
            CharacterCount = 12
        };

        var existingVectorDoc = new VectorDocumentEntity
        {
            Id = "vec-existing",
            GameId = "tic-tac-toe",
            PdfDocumentId = "pdf-3",
            ChunkCount = 1,
            TotalCharacters = 12,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow.AddDays(-1),
            EmbeddingModel = "openai/text-embedding-3-small",
            EmbeddingDimensions = 1536
        };

        _db.Users.Add(user);
        _db.Games.Add(game);
        _db.PdfDocuments.Add(pdf);
        _db.Set<VectorDocumentEntity>().Add(existingVectorDoc);
        await _db.SaveChangesAsync();

        // Mock services
        _mockChunkingService.Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<TextChunk> { new() { Text = "Test content", Index = 0, Page = 1, CharStart = 0, CharEnd = 12 } });

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[1536] }));

        _mockQdrantService.Setup(x => x.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockQdrantService.Setup(x => x.IndexDocumentChunksAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexResult.CreateSuccess(1));

        // WHEN: I trigger indexing again
        var result = await _service.IndexPdfAsync("pdf-3");

        // THEN: Should succeed and update the same VectorDocumentEntity
        Assert.True(result.Success);
        Assert.NotNull(result.VectorDocumentId);

        // Verify Qdrant delete was called (idempotency cleanup)
        _mockQdrantService.Verify(x => x.DeleteDocumentAsync("pdf-3", It.IsAny<CancellationToken>()), Times.Once);

        // Should have only ONE VectorDocumentEntity for this PDF
        var vectorDocCount = await _db.Set<VectorDocumentEntity>()
            .CountAsync(v => v.PdfDocumentId == "pdf-3");
        Assert.Equal(1, vectorDocCount);
    }

    /// <summary>
    /// Scenario: Handle embedding service failure
    /// </summary>
    [Fact]
    public async Task IndexPdfAsync_EmbeddingFailure_ReturnsFailureWithError()
    {
        // GIVEN: A valid PDF
        var user = new UserEntity
        {
            Id = "user-4",
            Email = "test4@example.com",
            DisplayName = "Test User 4",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        var game = new GameEntity { Id = "tic-tac-toe", Name = "Tic-Tac-Toe", CreatedAt = DateTime.UtcNow };
        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-4",
            GameId = "tic-tac-toe",
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "user-4",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = "Test content",
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow,
            PageCount = 1,
            CharacterCount = 12
        };

        _db.Users.Add(user);
        _db.Games.Add(game);
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        // Mock chunking succeeds
        _mockChunkingService.Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<TextChunk> { new() { Text = "Test content", Index = 0, Page = 1, CharStart = 0, CharEnd = 12 } });

        // Mock embedding FAILS
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("API error: rate limit exceeded"));

        // WHEN: I trigger indexing
        var result = await _service.IndexPdfAsync("pdf-4");

        // THEN: Should fail with appropriate error code
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.EmbeddingFailed, result.ErrorCode);
        Assert.Contains("Embedding generation failed", result.ErrorMessage!);

        // AND: VectorDocumentEntity should be marked as "failed"
        var vectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == "pdf-4");

        Assert.NotNull(vectorDoc);
        Assert.Equal("failed", vectorDoc.IndexingStatus);
        Assert.NotNull(vectorDoc.IndexingError);
    }
}
