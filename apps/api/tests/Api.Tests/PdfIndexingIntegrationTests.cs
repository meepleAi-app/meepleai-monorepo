using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD Integration tests for AI-01: PDF indexing workflow
/// Tests the complete flow: PDF upload → text extraction → chunking → embedding → Qdrant indexing
/// </summary>
[Collection("IntegrationTests")]
public class PdfIndexingIntegrationTests : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime
{
    private readonly WebApplicationFactoryFixture _factory;
    private readonly HttpClient _client;
    private MeepleAiDbContext? _db;
    private string? _sessionToken;
    private string? _editorSessionToken;
    private string? _userSessionToken;

    public PdfIndexingIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    public async Task InitializeAsync()
    {
        // Get database context
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Login as admin for most tests
        _sessionToken = await LoginAsync("admin@meepleai.dev", "Demo123!");
        _editorSessionToken = await LoginAsync("editor@meepleai.dev", "Demo123!");
        _userSessionToken = await LoginAsync("user@meepleai.dev", "Demo123!");
    }

    public Task DisposeAsync()
    {
        _db?.Dispose();
        return Task.CompletedTask;
    }

    #region BDD Scenarios: Happy Path

    /// <summary>
    /// Scenario: Successfully index a PDF document for semantic search
    /// Given the PDF has extracted text
    /// When I trigger indexing for the PDF
    /// Then the system should chunk, embed, and index the document in Qdrant
    /// And the VectorDocumentEntity status should be "completed"
    /// </summary>
    [Fact]
    public async Task IndexPdf_WithValidExtractedText_IndexesSuccessfully()
    {
        // GIVEN: A PDF with extracted text
        var gameId = "tic-tac-toe";
        var pdfId = await CreatePdfWithExtractedTextAsync(gameId, "Players alternate marking X or O in a 3x3 grid. " +
            "The first player to get three marks in a row (horizontally, vertically, or diagonally) wins the game. " +
            "If all nine squares are filled and no player has three in a row, the game is a draw.");

        // WHEN: I trigger indexing for the PDF
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: The request should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.VectorDocumentId);
        Assert.True(result.ChunkCount > 0, "Should have created at least 1 chunk");
        Assert.NotNull(result.IndexedAt);

        // AND: The VectorDocumentEntity should be persisted with status "completed"
        var vectorDoc = await _db!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.Id == result.VectorDocumentId);

        Assert.NotNull(vectorDoc);
        Assert.Equal("completed", vectorDoc.IndexingStatus);
        Assert.Equal(gameId, vectorDoc.GameId);
        Assert.Equal(pdfId, vectorDoc.PdfDocumentId);
        Assert.True(vectorDoc.ChunkCount > 0);
        Assert.True(vectorDoc.TotalCharacters > 0);
        Assert.NotNull(vectorDoc.IndexedAt);
        Assert.Null(vectorDoc.IndexingError);
        Assert.Equal("openai/text-embedding-3-small", vectorDoc.EmbeddingModel);
        Assert.Equal(1536, vectorDoc.EmbeddingDimensions);
    }

    /// <summary>
    /// Scenario: Query indexed documents filtered by game
    /// Given the PDF has been successfully indexed
    /// When I search for content in that game
    /// Then the search results should include relevant chunks from that game only
    /// </summary>
    [Fact(Skip = "Requires real Qdrant and OpenRouter LLM - mock returns empty results")]
    public async Task SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults()
    {
        // GIVEN: PDFs indexed for two different games
        var tttPdfId = await CreateAndIndexPdfAsync("tic-tac-toe",
            "Players alternate marking X or O. Three in a row wins.");
        var chessPdfId = await CreateAndIndexPdfAsync("chess",
            "The knight moves in an L-shape. Checkmate ends the game.");

        // WHEN: I search in the tic-tac-toe game
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var searchResponse = await _client.PostAsJsonAsync("/agents/qa", new
        {
            gameId = "tic-tac-toe",
            query = "how do players win the game?"
        });

        // THEN: Results should only include tic-tac-toe content
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var searchResult = await searchResponse.Content.ReadAsStringAsync();
        // The response should reference tic-tac-toe concepts, not chess
        Assert.Contains("three in a row", searchResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("knight", searchResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkmate", searchResult, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region BDD Scenarios: Edge Cases

    /// <summary>
    /// Scenario: Index a very small PDF (less than one chunk)
    /// Given the PDF has only 100 characters of text
    /// When I trigger indexing
    /// Then the system should create exactly 1 chunk
    /// </summary>
    [Fact]
    public async Task IndexPdf_WithSmallText_CreatesOneChunk()
    {
        // GIVEN: A PDF with minimal text (< 512 chars)
        var gameId = "tic-tac-toe";
        var shortText = "X wins with three in a row."; // ~27 characters
        var pdfId = await CreatePdfWithExtractedTextAsync(gameId, shortText);

        // WHEN: I trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should create exactly 1 chunk
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ChunkCount);
        Assert.True(result.Success);
    }

    /// <summary>
    /// Scenario: Index a large PDF (thousands of chunks)
    /// Given the PDF has 50000 characters of text
    /// When I trigger indexing
    /// Then the system should create approximately 100 chunks
    /// </summary>
    [Fact]
    public async Task IndexPdf_WithLargeText_CreatesMultipleChunks()
    {
        // GIVEN: A large PDF (50k chars)
        var gameId = "chess";
        var largeText = GenerateLargeText(50000);
        var pdfId = await CreatePdfWithExtractedTextAsync(gameId, largeText);

        // WHEN: I trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should create many chunks (with 512 char chunks and 50 overlap, expect ~95-100 chunks)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        Assert.NotNull(result);
        Assert.True(result.ChunkCount >= 90, $"Expected at least 90 chunks, got {result.ChunkCount}");
        Assert.True(result.ChunkCount <= 110, $"Expected at most 110 chunks, got {result.ChunkCount}");
    }

    /// <summary>
    /// Scenario: Re-index an already indexed PDF (idempotency)
    /// Given the PDF has already been indexed
    /// When I trigger indexing again
    /// Then the system should re-index without creating duplicates
    /// </summary>
    [Fact]
    public async Task IndexPdf_AlreadyIndexed_ReindexesWithoutDuplicates()
    {
        // GIVEN: A PDF that has already been indexed
        var gameId = "tic-tac-toe";
        var pdfId = await CreateAndIndexPdfAsync(gameId, "Original text about game rules.");

        var vectorDoc1 = await _db!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId);
        Assert.NotNull(vectorDoc1);
        var firstChunkCount = vectorDoc1.ChunkCount;

        // WHEN: I trigger indexing again
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed and update the same VectorDocumentEntity
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);

        // Should have only ONE VectorDocumentEntity for this PDF
        var vectorDocCount = await _db.Set<VectorDocumentEntity>()
            .CountAsync(v => v.PdfDocumentId == pdfId);
        Assert.Equal(1, vectorDocCount);
    }

    #endregion

    #region BDD Scenarios: Error Handling

    /// <summary>
    /// Scenario: Attempt to index a PDF without extracted text
    /// Given a PDF exists but text extraction has not completed
    /// When I trigger indexing
    /// Then the request should be rejected with HTTP 400
    /// </summary>
    [Fact]
    public async Task IndexPdf_WithoutExtractedText_ReturnsBadRequest()
    {
        // GIVEN: A PDF without extracted text
        var pdfId = await CreatePdfWithoutExtractedTextAsync("tic-tac-toe");

        // WHEN: I trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(errorResponse);
        Assert.Contains("text extraction", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Attempt to index a non-existent PDF
    /// When I trigger indexing for a PDF that doesn't exist
    /// Then the request should be rejected with HTTP 404
    /// </summary>
    [Fact]
    public async Task IndexPdf_NonExistentPdf_ReturnsNotFound()
    {
        // GIVEN: A non-existent PDF ID
        var fakePdfId = "non-existent-pdf-id";

        // WHEN: I trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{fakePdfId}/index", null);

        // THEN: Should return 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region BDD Scenarios: Authorization

    /// <summary>
    /// Scenario: Admin can trigger indexing
    /// Given I am authenticated as an Admin user
    /// When I trigger indexing
    /// Then the indexing should proceed successfully
    /// </summary>
    [Fact]
    public async Task IndexPdf_AsAdmin_Succeeds()
    {
        // GIVEN: Authenticated as admin
        var pdfId = await CreatePdfWithExtractedTextAsync("tic-tac-toe", "Test content");

        // WHEN: Trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor can trigger indexing
    /// Given I am authenticated as an Editor user
    /// When I trigger indexing
    /// Then the indexing should proceed successfully
    /// </summary>
    [Fact]
    public async Task IndexPdf_AsEditor_Succeeds()
    {
        // GIVEN: Authenticated as editor
        var pdfId = await CreatePdfWithExtractedTextAsync("tic-tac-toe", "Test content");

        // WHEN: Trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _editorSessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Regular user cannot trigger indexing
    /// Given I am authenticated as a regular User
    /// When I attempt to trigger indexing
    /// Then the request should be rejected with HTTP 403 Forbidden
    /// </summary>
    [Fact]
    public async Task IndexPdf_AsRegularUser_ReturnsForbidden()
    {
        // GIVEN: Authenticated as regular user
        var pdfId = await CreatePdfWithExtractedTextAsync("tic-tac-toe", "Test content");

        // WHEN: Attempt to trigger indexing
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userSessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated request is rejected
    /// Given I am not authenticated
    /// When I attempt to trigger indexing
    /// Then the request should be rejected with HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task IndexPdf_Unauthenticated_ReturnsUnauthorized()
    {
        // GIVEN: No authentication
        var pdfId = await CreatePdfWithExtractedTextAsync("tic-tac-toe", "Test content");

        // WHEN: Attempt to trigger indexing
        _client.DefaultRequestHeaders.Clear();
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task<string> LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);

        // Return the full cookie string (name=value), not just the value
        var sessionCookie = setCookieHeader.Split(';')[0];
        return sessionCookie;
    }

    private async Task<string> CreatePdfWithExtractedTextAsync(string gameId, string extractedText)
    {
        var pdfId = $"test-pdf-{Guid.NewGuid():N}";

        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = $"{pdfId}.pdf",
            FilePath = $"/pdfs/{pdfId}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "demo-admin-001",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = extractedText,
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow,
            PageCount = 1,
            CharacterCount = extractedText.Length
        };

        _db!.Set<PdfDocumentEntity>().Add(pdf);
        await _db.SaveChangesAsync();

        return pdfId;
    }

    private async Task<string> CreatePdfWithoutExtractedTextAsync(string gameId)
    {
        var pdfId = $"test-pdf-notext-{Guid.NewGuid():N}";

        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = $"{pdfId}.pdf",
            FilePath = $"/pdfs/{pdfId}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "demo-admin-001",
            UploadedAt = DateTime.UtcNow,
            ExtractedText = null,
            ProcessingStatus = "pending",
            ProcessedAt = null,
            PageCount = 0,
            CharacterCount = 0
        };

        _db!.Set<PdfDocumentEntity>().Add(pdf);
        await _db.SaveChangesAsync();

        return pdfId;
    }

    private async Task<string> CreateAndIndexPdfAsync(string gameId, string text)
    {
        var pdfId = await CreatePdfWithExtractedTextAsync(gameId, text);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);
        response.EnsureSuccessStatusCode();

        return pdfId;
    }

    private static string GenerateLargeText(int characterCount)
    {
        const string template = "This is a sample sentence for testing large document indexing. " +
            "It contains information about game rules, player actions, and scoring mechanisms. " +
            "The text is repeated to create a document of the desired length. ";

        var sb = new System.Text.StringBuilder(characterCount);
        while (sb.Length < characterCount)
        {
            sb.Append(template);
        }

        return sb.ToString(0, characterCount);
    }

    #endregion

    #region Response Models

    private record PdfIndexingResponse(
        bool Success,
        string? VectorDocumentId,
        int ChunkCount,
        DateTime? IndexedAt);

    private record ErrorResponse(string Error);

    #endregion
}
