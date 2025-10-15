using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for PDF upload endpoints (PDF-01).
///
/// Feature: PDF-01 - Upload PDF regolamenti
/// As an Admin/Editor
/// I want to upload PDF rulebooks
/// So that they are available for processing and indexing
/// </summary>
public class PdfUploadEndpointsTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PdfUploadEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Upload PDF valido con successo
    ///   Given l'utente è autenticato come Admin
    ///   And esiste un gioco "chess"
    ///   When l'utente carica un PDF valido
    ///   Then il sistema restituisce HTTP 200
    ///   And il PDF viene salvato nel database
    ///   And viene restituito documentId e fileName
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenAdminUploadsValidPdf_ReturnsSuccessWithDocumentId()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-admin", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Chess-{TestRunId}");

        // When: Admin uploads a valid PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\nfake pdf content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "chess-rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with documentId and fileName
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("documentId", out var docId));
        Assert.True(result.TryGetProperty("fileName", out var fileName));
        Assert.False(string.IsNullOrWhiteSpace(docId.GetString()));
        Assert.Equal("chess-rules.pdf", fileName.GetString());

        // And: PDF is saved in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.FirstOrDefaultAsync(p => p.Id == docId.GetString());
        Assert.NotNull(pdf);
        Assert.Equal(game.Id, pdf!.GameId);
        Assert.Equal(admin.Id, pdf.UploadedByUserId);
        Assert.Equal("pending", pdf.ProcessingStatus);

        TrackPdfDocumentId(docId.GetString()!);
    }

    /// <summary>
    /// Scenario: Editor può caricare PDF
    ///   Given l'utente è autenticato come Editor
    ///   When l'utente carica un PDF
    ///   Then il sistema restituisce HTTP 200
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenEditorUploadsValidPdf_ReturnsSuccess()
    {
        // Given: Editor user is authenticated
        var editor = await CreateTestUserAsync("pdf-editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Monopoly-{TestRunId}");

        // When: Editor uploads a valid PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\nfake pdf"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("documentId", out var docId));
        TrackPdfDocumentId(docId.GetString()!);
    }

    /// <summary>
    /// Scenario: Upload fallisce - utente non autenticato
    ///   Given l'utente NON è autenticato
    ///   When l'utente tenta di caricare un PDF
    ///   Then il sistema restituisce HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        var client = CreateClientWithoutCookies();
        var game = await CreateTestGameAsync("Test Game");

        // When: User attempts to upload PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var response = await client.PostAsync("/api/v1/ingest/pdf", content);

        // Then: HTTP 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Upload fallisce - utente senza permessi (User role)
    ///   Given l'utente è autenticato con ruolo "User"
    ///   When l'utente tenta di caricare un PDF
    ///   Then il sistema restituisce HTTP 403 Forbidden
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenUserRoleAttempts_ReturnsForbidden()
    {
        // Given: User with "User" role is authenticated
        var user = await CreateTestUserAsync("regular-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var game = await CreateTestGameAsync("Test Game");

        // When: User attempts to upload PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Upload fallisce - gameId mancante
    ///   Given l'utente è autenticato come Admin
    ///   When l'utente carica un PDF senza specificare gameId
    ///   Then il sistema restituisce HTTP 400
    ///   And il messaggio di errore contiene "gameId is required"
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenGameIdMissing_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-admin-2", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin uploads PDF without gameId
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        // gameId intentionally omitted

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with error message
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(error.TryGetProperty("error", out var errorMsg));
        Assert.Contains("gameId is required", errorMsg.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Lista PDF per gioco
    ///   Given esistono 3 PDF caricati per il game "chess"
    ///   When l'utente richiede la lista PDF per "chess"
    ///   Then il sistema restituisce HTTP 200
    ///   And la risposta contiene 3 PDF ordinati per uploadedAt discendente
    /// </summary>
    [Fact]
    public async Task GetGamesPdfs_WhenMultiplePdfsExist_ReturnsOrderedList()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-list-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with 3 PDFs exists
        var game = await CreateTestGameAsync("Chess List");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var pdf1 = new PdfDocumentEntity
            {
                Id = $"pdf-list-1-{TestRunId}",
                GameId = game.Id,
                FileName = "rules-v1.pdf",
                FilePath = "/tmp/v1.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow.AddHours(-2),
                ProcessingStatus = "completed"
            };
            var pdf2 = new PdfDocumentEntity
            {
                Id = $"pdf-list-2-{TestRunId}",
                GameId = game.Id,
                FileName = "rules-v2.pdf",
                FilePath = "/tmp/v2.pdf",
                FileSizeBytes = 2048,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow.AddHours(-1),
                ProcessingStatus = "pending"
            };
            var pdf3 = new PdfDocumentEntity
            {
                Id = $"pdf-list-3-{TestRunId}",
                GameId = game.Id,
                FileName = "rules-v3.pdf",
                FilePath = "/tmp/v3.pdf",
                FileSizeBytes = 3072,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "failed"
            };

            db.PdfDocuments.AddRange(pdf1, pdf2, pdf3);
            await db.SaveChangesAsync();

            TrackPdfDocumentId(pdf1.Id);
            TrackPdfDocumentId(pdf2.Id);
            TrackPdfDocumentId(pdf3.Id);
        }

        // When: User requests PDF list for game
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/pdfs");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with 3 PDFs ordered by uploadedAt descending
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("pdfs", out var pdfsElement));

        var pdfs = pdfsElement.EnumerateArray().ToList();
        Assert.Equal(3, pdfs.Count);

        // Verify ordering: most recent first
        Assert.Equal("rules-v3.pdf", pdfs[0].GetProperty("fileName").GetString());
        Assert.Equal("rules-v2.pdf", pdfs[1].GetProperty("fileName").GetString());
        Assert.Equal("rules-v1.pdf", pdfs[2].GetProperty("fileName").GetString());

        // Verify required fields
        Assert.True(pdfs[0].TryGetProperty("id", out _));
        Assert.True(pdfs[0].TryGetProperty("fileSizeBytes", out _));
        Assert.True(pdfs[0].TryGetProperty("uploadedAt", out _));
        Assert.True(pdfs[0].TryGetProperty("uploadedByUserId", out _));
    }

    /// <summary>
    /// Scenario: Lista PDF per gioco senza PDF
    ///   Given NON esistono PDF per il game "new-game"
    ///   When l'utente richiede la lista PDF per "new-game"
    ///   Then il sistema restituisce HTTP 200
    ///   And la risposta contiene un array vuoto
    /// </summary>
    [Fact]
    public async Task GetGamesPdfs_WhenNoPdfsExist_ReturnsEmptyArray()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-empty-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game without PDFs exists
        var game = await CreateTestGameAsync("Empty Game");

        // When: User requests PDF list
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/pdfs");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with empty array
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("pdfs", out var pdfsElement));

        var pdfs = pdfsElement.EnumerateArray().ToList();
        Assert.Empty(pdfs);
    }

    /// <summary>
    /// Scenario: Lista PDF richiede autenticazione
    ///   Given l'utente NON è autenticato
    ///   When l'utente richiede la lista PDF
    ///   Then il sistema restituisce HTTP 401
    /// </summary>
    [Fact]
    public async Task GetGamesPdfs_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        var client = CreateClientWithoutCookies();
        var game = await CreateTestGameAsync("Test Game");

        // When: User requests PDF list
        var response = await client.GetAsync($"/api/v1/games/{game.Id}/pdfs");

        // Then: HTTP 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Dettagli elaborazione PDF - completed
    ///   Given un PDF con status "completed" esiste
    ///   When l'utente richiede i dettagli del PDF
    ///   Then il sistema restituisce HTTP 200
    ///   And la risposta include extractedText e pageCount
    /// </summary>
    [Fact]
    public async Task GetPdfText_WhenProcessingCompleted_ReturnsExtractedText()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-text-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A PDF with completed processing exists
        var game = await CreateTestGameAsync("Completed PDF Game");
        var pdfId = $"pdf-completed-{TestRunId}";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var pdf = new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = game.Id,
                FileName = "completed-rules.pdf",
                FilePath = "/tmp/completed.pdf",
                FileSizeBytes = 4096,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "completed",
                ExtractedText = "This is the extracted text from the PDF rulebook.",
                PageCount = 10,
                CharacterCount = 500,
                ProcessedAt = DateTime.UtcNow
            };

            db.PdfDocuments.Add(pdf);
            await db.SaveChangesAsync();
            TrackPdfDocumentId(pdfId);
        }

        // When: User requests PDF details
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pdfs/{pdfId}/text");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with extracted text details
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("processingStatus", out var status));
        Assert.Equal("completed", status.GetString());
        Assert.True(result.TryGetProperty("extractedText", out var text));
        Assert.Contains("extracted text", text.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(result.TryGetProperty("pageCount", out var pageCount));
        Assert.Equal(10, pageCount.GetInt32());
        Assert.True(result.TryGetProperty("characterCount", out var charCount));
        Assert.Equal(500, charCount.GetInt32());
    }

    /// <summary>
    /// Scenario: Dettagli elaborazione PDF - pending
    ///   Given un PDF con status "pending" esiste
    ///   When l'utente richiede i dettagli del PDF
    ///   Then il sistema restituisce HTTP 200
    ///   And extractedText è null
    /// </summary>
    [Fact]
    public async Task GetPdfText_WhenProcessingPending_ReturnsNullExtractedText()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-pending-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A PDF with pending processing exists
        var game = await CreateTestGameAsync("Pending PDF Game");
        var pdfId = $"pdf-pending-{TestRunId}";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var pdf = new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = game.Id,
                FileName = "pending-rules.pdf",
                FilePath = "/tmp/pending.pdf",
                FileSizeBytes = 2048,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "pending"
            };

            db.PdfDocuments.Add(pdf);
            await db.SaveChangesAsync();
            TrackPdfDocumentId(pdfId);
        }

        // When: User requests PDF details
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pdfs/{pdfId}/text");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with null extractedText
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("processingStatus", out var status));
        Assert.Equal("pending", status.GetString());

        // extractedText should be null or missing
        if (result.TryGetProperty("extractedText", out var text))
        {
            Assert.Equal(JsonValueKind.Null, text.ValueKind);
        }
    }

    /// <summary>
    /// Scenario: Dettagli elaborazione PDF - failed
    ///   Given un PDF con status "failed" esiste
    ///   When l'utente richiede i dettagli del PDF
    ///   Then il sistema restituisce HTTP 200
    ///   And processingError contiene il messaggio di errore
    /// </summary>
    [Fact]
    public async Task GetPdfText_WhenProcessingFailed_ReturnsErrorMessage()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-failed-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A PDF with failed processing exists
        var game = await CreateTestGameAsync("Failed PDF Game");
        var pdfId = $"pdf-failed-{TestRunId}";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var pdf = new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = game.Id,
                FileName = "failed-rules.pdf",
                FilePath = "/tmp/failed.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "failed",
                ProcessingError = "Corrupted PDF file",
                ProcessedAt = DateTime.UtcNow
            };

            db.PdfDocuments.Add(pdf);
            await db.SaveChangesAsync();
            TrackPdfDocumentId(pdfId);
        }

        // When: User requests PDF details
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pdfs/{pdfId}/text");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with error message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("processingStatus", out var status));
        Assert.Equal("failed", status.GetString());
        Assert.True(result.TryGetProperty("processingError", out var error));
        Assert.Equal("Corrupted PDF file", error.GetString());
    }

    /// <summary>
    /// Scenario: Dettagli PDF - not found
    ///   Given un PDF con l'ID specificato NON esiste
    ///   When l'utente richiede i dettagli del PDF
    ///   Then il sistema restituisce HTTP 404
    /// </summary>
    [Fact]
    public async Task GetPdfText_WhenPdfNotFound_ReturnsNotFound()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("pdf-notfound-user", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests details for non-existent PDF
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pdfs/nonexistent-pdf-id/text");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(error.TryGetProperty("error", out var errorMsg));
        Assert.Contains("not found", errorMsg.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Dettagli PDF richiede autenticazione
    ///   Given l'utente NON è autenticato
    ///   When l'utente richiede i dettagli di un PDF
    ///   Then il sistema restituisce HTTP 401
    /// </summary>
    [Fact]
    public async Task GetPdfText_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        var client = CreateClientWithoutCookies();

        // When: User requests PDF details
        var response = await client.GetAsync("/api/v1/pdfs/some-pdf-id/text");

        // Then: HTTP 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
