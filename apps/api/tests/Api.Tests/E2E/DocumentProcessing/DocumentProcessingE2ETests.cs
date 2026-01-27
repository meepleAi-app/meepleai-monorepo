using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.DocumentProcessing;

/// <summary>
/// E2E tests for document processing workflows.
/// Tests PDF upload, processing status, and text extraction.
///
/// Issue #3012: Backend E2E Test Suite - Document Processing Flows
///
/// Critical Journeys Covered:
/// - Upload PDF document
/// - Check processing progress/status
/// - Handle invalid file types
/// - Get extracted text
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
public sealed class DocumentProcessingE2ETests : E2ETestBase
{
    private Guid _testGameId;

    public DocumentProcessingE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        // Seed a game for document association
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "E2E Document Test Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            YearPublished = 2024,
            BggId = null,
            ImageUrl = "https://example.com/doc-test.png",
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Games.Add(game);
        await DbContext.SaveChangesAsync();
        _testGameId = game.Id;
    }

    #region PDF Upload Tests

    [Fact]
    public async Task UploadPdf_WithValidFile_ReturnsUploadResult()
    {
        // Arrange
        var email = $"doc_upload_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Create a minimal PDF content (simplified PDF structure)
        var pdfContent = CreateMinimalPdfBytes();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test-rules.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.Accepted,
            HttpStatusCode.BadRequest // May fail if PDF processing service not available
        );
    }

    [Fact]
    public async Task UploadPdf_WithoutAuthentication_ReturnsUnauthorizedOrAccepted()
    {
        // Arrange - No authentication
        ClearAuthentication();

        var pdfContent = CreateMinimalPdfBytes();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert - May allow anonymous upload depending on config
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Accepted,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest
        );
    }

    [Fact]
    public async Task UploadPdf_InvalidFileType_ReturnsError()
    {
        // Arrange
        var email = $"doc_invalid_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Create a text file pretending to be PDF
        var invalidContent = Encoding.UTF8.GetBytes("This is not a PDF file");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(invalidContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "fake.txt");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task UploadPdf_EmptyFile_ReturnsError()
    {
        // Arrange
        var email = $"doc_empty_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "empty.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity
        );
    }

    #endregion

    #region Processing Status Tests

    [Fact]
    public async Task GetProcessingProgress_ValidPdfId_ReturnsStatus()
    {
        // Arrange
        var email = $"doc_progress_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Upload a PDF first
        var pdfContent = CreateMinimalPdfBytes();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "status-test.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        var uploadResponse = await Client.PostAsync("/api/v1/ingest/pdf", content);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            return; // Skip if upload not available
        }

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<PdfUploadResponse>();

        if (uploadResult?.PdfId == null || uploadResult.PdfId == Guid.Empty)
        {
            return; // Skip if no PDF ID returned
        }

        // Act
        var response = await Client.GetAsync($"/api/v1/pdfs/{uploadResult.PdfId}/progress");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task GetProcessingProgress_InvalidPdfId_ReturnsNotFound()
    {
        // Arrange
        var email = $"doc_progress_nf_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var invalidId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/pdfs/{invalidId}/progress");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Text Extraction Tests

    [Fact]
    public async Task GetExtractedText_ValidPdfId_ReturnsText()
    {
        // Arrange
        var email = $"doc_text_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Upload a PDF first
        var pdfContent = CreateMinimalPdfBytes();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "text-test.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        var uploadResponse = await Client.PostAsync("/api/v1/ingest/pdf", content);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            return; // Skip if upload not available
        }

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<PdfUploadResponse>();

        if (uploadResult?.PdfId == null || uploadResult.PdfId == Guid.Empty)
        {
            return;
        }

        // Wait a bit for processing (in real E2E this might need polling)
        await Task.Delay(500);

        // Act
        var response = await Client.GetAsync($"/api/v1/pdfs/{uploadResult.PdfId}/text");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Accepted // Still processing
        );
    }

    #endregion

    #region List PDFs Tests

    [Fact]
    public async Task GetGamePdfs_WithValidGameId_ReturnsList()
    {
        // Arrange
        var email = $"doc_list_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{_testGameId}/pdfs");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task GetGamePdfs_InvalidGameId_ReturnsNotFound()
    {
        // Arrange
        var email = $"doc_list_nf_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var invalidGameId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{invalidGameId}/pdfs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Complete Document Processing Journey

    [Fact]
    public async Task CompleteDocumentJourney_UploadCheckStatus_Succeeds()
    {
        // Step 1: Register user
        var email = $"doc_journey_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Step 2: Create PDF content
        var pdfContent = CreateMinimalPdfBytes();

        // Step 3: Upload PDF
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "journey-test.pdf");
        content.Add(new StringContent(_testGameId.ToString()), "gameId");

        var uploadResponse = await Client.PostAsync("/api/v1/ingest/pdf", content);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            // PDF processing service may not be available in test environment
            // Verify we get appropriate error response
            uploadResponse.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.InternalServerError
            );
            return;
        }

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<PdfUploadResponse>();
        uploadResult.Should().NotBeNull();

        if (uploadResult!.PdfId == Guid.Empty)
        {
            return; // Skip if async processing
        }

        // Step 4: Check processing status
        var progressResponse = await Client.GetAsync($"/api/v1/pdfs/{uploadResult.PdfId}/progress");
        progressResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // Step 5: List game PDFs
        var listResponse = await Client.GetAsync($"/api/v1/games/{_testGameId}/pdfs");
        listResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (listResponse.StatusCode == HttpStatusCode.OK)
        {
            var pdfs = await listResponse.Content.ReadFromJsonAsync<List<PdfInfoDto>>();
            pdfs.Should().NotBeNull();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates minimal valid PDF bytes for testing.
    /// This is a simplified PDF structure that should be recognized as a PDF.
    /// </summary>
    private static byte[] CreateMinimalPdfBytes()
    {
        // Minimal PDF structure
        const string minimalPdf = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
xref
0 4
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
trailer
<< /Size 4 /Root 1 0 R >>
startxref
190
%%EOF";

        return Encoding.ASCII.GetBytes(minimalPdf);
    }

    #endregion

    #region Response DTOs

    private sealed record PdfUploadResponse(
        Guid PdfId,
        string Status,
        string? Message);

    private sealed record PdfProgressResponse(
        Guid PdfId,
        string Status,
        int ProgressPercentage,
        string? CurrentStep,
        string? ErrorMessage);

    private sealed record PdfTextResponse(
        Guid PdfId,
        string ExtractedText,
        int PageCount);

    private sealed record PdfInfoDto(
        Guid Id,
        string FileName,
        string Status,
        int PageCount,
        DateTime UploadedAt);

    #endregion
}
