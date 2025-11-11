using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Tests.Fixtures;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// PDF-09: BDD-style integration tests for PDF upload validation
///
/// Feature: PDF-09 - Pre-upload PDF validation
/// As an Admin/Editor
/// I want the system to validate PDF files before upload
/// So that invalid files are rejected with clear error messages
/// </summary>
[Collection("Postgres Integration Tests")]
public class PdfUploadValidationIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PdfUploadValidationIntegrationTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    /// <summary>
    /// Helper method to load real PDF fixture from Fixtures directory.
    /// This prevents flaky tests caused by fake PDFs that crash Docnet.Core during background processing.
    /// See issue #490 for details.
    /// </summary>
    private static async Task<byte[]> LoadRealPdfFixtureAsync()
    {
        var fixturePath = Path.Combine("Fixtures", "sample_table.pdf");
        return await File.ReadAllBytesAsync(fixturePath);
    }

    /// <summary>
    /// Scenario: Upload fails - file size exceeds maximum
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user uploads a PDF larger than 100MB
    ///   Then the system returns HTTP 400
    ///   And the error response contains "validation_failed"
    ///   And the details include a fileSize error message
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenFileSizeExceedsMaximum_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-1", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-1-{TestRunId}");

        // When: Admin uploads an oversized PDF (simulate with large content)
        var content = new MultipartFormDataContent();
        // 101 MB file (exceeds 100 MB limit)
        var oversizedContent = new byte[101 * 1024 * 1024];
        Array.Fill<byte>(oversizedContent, 0x25); // Fill with '%' character
        var fileContent = new ByteArrayContent(oversizedContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "oversized-rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with validation_failed error
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");

        result.TryGetProperty("details", out var details).Should().BeTrue();
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        detailsDict.Should().NotBeNull();
        detailsDict!.Keys.Should().Contain("fileSize");
        detailsDict["fileSize"].Should().Contain("exceeds maximum");
    }

    /// <summary>
    /// Scenario: Upload fails - invalid MIME type
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user uploads a file with MIME type "application/msword"
    ///   Then the system returns HTTP 400
    ///   And the error details include a fileType error
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenMimeTypeIsInvalid_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-2", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-2-{TestRunId}");

        // When: Admin uploads a file with wrong MIME type
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\ntest content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/msword"); // Wrong MIME type
        content.Add(fileContent, "file", "rules.doc");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with validation error
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");

        result.TryGetProperty("details", out var details).Should().BeTrue();
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        detailsDict.Should().NotBeNull();
        detailsDict!.Keys.Should().Contain("fileType");
        detailsDict["fileType"].Should().Contain("not allowed");
    }

    /// <summary>
    /// Scenario: Upload fails - invalid PDF magic bytes
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user uploads a file that doesn't start with PDF magic bytes
    ///   Then the system returns HTTP 400
    ///   And the error details include a fileFormat error
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenMagicBytesAreInvalid_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-3", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-3-{TestRunId}");

        // When: Admin uploads a file with invalid PDF magic bytes
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("This is not a PDF file at all"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "fake.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with format validation error
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");

        result.TryGetProperty("details", out var details).Should().BeTrue();
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        detailsDict.Should().NotBeNull();
        detailsDict!.Keys.Should().Contain("fileFormat");
        detailsDict["fileFormat"].Should().Contain("Invalid PDF file format");
    }

    /// <summary>
    /// Scenario: Upload fails - empty file
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user uploads an empty file
    ///   Then the system returns HTTP 400
    ///   And the error details include a file error
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenFileIsEmpty_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-4", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-4-{TestRunId}");

        // When: Admin uploads an empty file
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "empty.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");
    }

    /// <summary>
    /// Scenario: Upload fails - no file provided
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user submits upload request without a file
    ///   Then the system returns HTTP 400
    ///   And the error mentions no file provided
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenNoFileProvided_ReturnsBadRequest()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-5", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-5-{TestRunId}");

        // When: Admin submits upload without file
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(game.Id), "gameId");
        // No file added

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");

        result.TryGetProperty("details", out var details).Should().BeTrue();
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        detailsDict.Should().NotBeNull();
        detailsDict!.Keys.Should().Contain("file");
        detailsDict["file"].Should().Contain("No file provided");
    }

    /// <summary>
    /// Scenario: Upload succeeds - valid PDF with correct size and format
    ///   Given the user is authenticated as Admin
    ///   And a game exists
    ///   When the user uploads a valid PDF under 100MB
    ///   Then the system returns HTTP 200
    ///   And the PDF is saved to database
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenPdfIsValid_ReturnsSuccess()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-6", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-6-{TestRunId}");

        // When: Admin uploads a valid PDF (real PDF fixture to prevent flaky tests - issue #490)
        var content = new MultipartFormDataContent();
        var pdfBytes = await LoadRealPdfFixtureAsync();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "valid-rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with documentId
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("documentId", out var docId).Should().BeTrue();
        string.IsNullOrWhiteSpace(docId.GetString()).Should().BeFalse();

        TrackPdfDocumentId(docId.GetString()!);
    }

    /// <summary>
    /// Scenario: Validation error response structure is correct
    ///   Given the user uploads an invalid file
    ///   When validation fails
    ///   Then the error response has correct structure
    ///   And includes error type and details dictionary
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_ValidationErrorResponse_HasCorrectStructure()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("pdf-val-admin-7", "admin");
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-7-{TestRunId}");

        // When: Admin uploads invalid file
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("not a pdf"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "invalid.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Error response has correct structure
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify top-level error field
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Be("validation_failed");

        // Verify details field exists and is an object
        result.TryGetProperty("details", out var details).Should().BeTrue();
        details.ValueKind.Should().Be(JsonValueKind.Object);

        // Verify details can be deserialized to Dictionary<string, string>
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        detailsDict.Should().NotBeNull();
        detailsDict!.Should().NotBeEmpty();
    }

    /// <summary>
    /// Scenario: Editor can upload valid PDF (authorization check)
    ///   Given the user is authenticated as Editor
    ///   When the user uploads a valid PDF
    ///   Then the validation and upload succeed
    /// </summary>
    [Fact]
    public async Task PostIngestPdf_WhenEditorUploadsValidPdf_PassesValidationAndSucceeds()
    {
        // Given: Editor user is authenticated
        var editor = await CreateTestUserAsync("pdf-val-editor-1", "editor");
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-Editor-{TestRunId}");

        // When: Editor uploads valid PDF (real PDF fixture to prevent flaky tests - issue #490)
        var content = new MultipartFormDataContent();
        var pdfBytes = await LoadRealPdfFixtureAsync();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "editor-rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("documentId", out var docId).Should().BeTrue();
        TrackPdfDocumentId(docId.GetString()!);
    }
}
