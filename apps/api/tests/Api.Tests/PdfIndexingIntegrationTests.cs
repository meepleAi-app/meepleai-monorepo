using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD Integration tests for AI-01: PDF indexing workflow
/// Tests the complete flow: PDF upload → text extraction → chunking → embedding → Qdrant indexing
/// </summary>
[Collection("IntegrationTests")]
public class PdfIndexingIntegrationTests : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    private readonly WebApplicationFactoryFixture _factory;
    private readonly HttpClient _client;
    private MeepleAiDbContext? _db;
    private string? _sessionToken;
    private string? _editorSessionToken;
    private string? _userSessionToken;

    public PdfIndexingIntegrationTests(WebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        _output = output;
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: The request should succeed
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.VectorDocumentId.Should().NotBeNull();
        (result.ChunkCount > 0).Should().BeTrue("Should have created at least 1 chunk");
        result.IndexedAt.Should().NotBeNull();

        // AND: The VectorDocumentEntity should be persisted with status "completed"
        var vectorDoc = await _db!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.Id == result.VectorDocumentId);

        vectorDoc.Should().NotBeNull();
        vectorDoc.IndexingStatus.Should().Be("completed");
        vectorDoc.GameId.Should().Be(gameId);
        vectorDoc.PdfDocumentId.Should().Be(pdfId);
        (vectorDoc.ChunkCount > 0).Should().BeTrue();
        (vectorDoc.TotalCharacters > 0).Should().BeTrue();
        vectorDoc.IndexedAt.Should().NotBeNull();
        vectorDoc.IndexingError.Should().BeNull();
        vectorDoc.EmbeddingModel.Should().Be("openai/text-embedding-3-small");
        vectorDoc.EmbeddingDimensions.Should().Be(1536);
    }

    /// <summary>
    /// Scenario: Query indexed documents filtered by game
    /// Given the PDF has been successfully indexed
    /// When I search for content in that game
    /// Then the search results should include relevant chunks from that game only
    /// </summary>
    [Fact]
    public async Task SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults()
    {
        // GIVEN: PDFs indexed for two different games
        var ticTacToeGame = await CreateGameAsync("Tic-Tac-Toe");
        var chessGame = await CreateGameAsync("Chess");

        var tttPdfId = await CreateAndIndexPdfAsync(ticTacToeGame.Id,
            "Players alternate marking X or O. Three in a row wins.");
        var chessPdfId = await CreateAndIndexPdfAsync(chessGame.Id,
            "The knight moves in an L-shape. Checkmate ends the game.");

        // WHEN: I search in the tic-tac-toe game
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var searchResponse = await _client.PostAsJsonAsync("/api/v1/agents/qa", new
        {
            gameId = ticTacToeGame.Id,
            query = "how do players win the game?"
        });

        // THEN: Results should only include tic-tac-toe content
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResult = await searchResponse.Content.ReadAsStringAsync();
        // The response should reference tic-tac-toe concepts, not chess
        searchResult.Should().Contain("three in a row");
        searchResult.Should().NotContain("knight", StringComparison.OrdinalIgnoreCase);
        searchResult.Should().NotContain("checkmate", StringComparison.OrdinalIgnoreCase);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should create exactly 1 chunk
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        result.Should().NotBeNull();
        result.ChunkCount.Should().Be(1);
        result.Success.Should().BeTrue();
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should create many chunks (with 512 char chunks and 50 overlap, expect ~95-120 chunks)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        result.Should().NotBeNull();
        (result.ChunkCount >= 90).Should().BeTrue($"Expected at least 90 chunks, got {result.ChunkCount}");
        (result.ChunkCount <= 130).Should().BeTrue($"Expected at most 130 chunks, got {result.ChunkCount}");
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
        vectorDoc1.Should().NotBeNull();
        var firstChunkCount = vectorDoc1.ChunkCount;

        // WHEN: I trigger indexing again
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _sessionToken);
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed and update the same VectorDocumentEntity
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PdfIndexingResponse>();
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Should have only ONE VectorDocumentEntity for this PDF
        var vectorDocCount = await _db.Set<VectorDocumentEntity>()
            .CountAsync(v => v.PdfDocumentId == pdfId);
        vectorDocCount.Should().Be(1);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Contain("text extraction");
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{fakePdfId}/index", null);

        // THEN: Should return 404 Not Found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should succeed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);

        // THEN: Should return 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task<string> LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();

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
        var response = await _client.PostAsync($"/api/v1/ingest/pdf/{pdfId}/index", null);
        response.EnsureSuccessStatusCode();

        return pdfId;
    }

    private async Task<GameEntity> CreateGameAsync(string name)
    {
        var game = new GameEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

        _db!.Games.Add(game);
        await _db.SaveChangesAsync();
        return game;
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
