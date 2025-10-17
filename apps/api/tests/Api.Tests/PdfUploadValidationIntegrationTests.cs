using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
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
public class PdfUploadValidationIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PdfUploadValidationIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
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
        var admin = await CreateTestUserAsync("pdf-val-admin-1", UserRole.Admin);
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with validation_failed error
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());

        Assert.True(result.TryGetProperty("details", out var details));
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        Assert.NotNull(detailsDict);
        Assert.Contains("fileSize", detailsDict!.Keys);
        Assert.Contains("exceeds maximum", detailsDict["fileSize"]);
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
        var admin = await CreateTestUserAsync("pdf-val-admin-2", UserRole.Admin);
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with validation error
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());

        Assert.True(result.TryGetProperty("details", out var details));
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        Assert.NotNull(detailsDict);
        Assert.Contains("fileType", detailsDict!.Keys);
        Assert.Contains("not allowed", detailsDict["fileType"]);
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
        var admin = await CreateTestUserAsync("pdf-val-admin-3", UserRole.Admin);
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400 with format validation error
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());

        Assert.True(result.TryGetProperty("details", out var details));
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        Assert.NotNull(detailsDict);
        Assert.Contains("fileFormat", detailsDict!.Keys);
        Assert.Contains("Invalid PDF file format", detailsDict["fileFormat"]);
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
        var admin = await CreateTestUserAsync("pdf-val-admin-4", UserRole.Admin);
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());
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
        var admin = await CreateTestUserAsync("pdf-val-admin-5", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-5-{TestRunId}");

        // When: Admin submits upload without file
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(game.Id), "gameId");
        // No file added

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());

        Assert.True(result.TryGetProperty("details", out var details));
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        Assert.NotNull(detailsDict);
        Assert.Contains("file", detailsDict!.Keys);
        Assert.Contains("No file provided", detailsDict["file"]);
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
        var admin = await CreateTestUserAsync("pdf-val-admin-6", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-6-{TestRunId}");

        // When: Admin uploads a valid PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n%valid pdf content here"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "valid-rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with documentId
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("documentId", out var docId));
        Assert.False(string.IsNullOrWhiteSpace(docId.GetString()));

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
        var admin = await CreateTestUserAsync("pdf-val-admin-7", UserRole.Admin);
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = content
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Error response has correct structure
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify top-level error field
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("validation_failed", error.GetString());

        // Verify details field exists and is an object
        Assert.True(result.TryGetProperty("details", out var details));
        Assert.Equal(JsonValueKind.Object, details.ValueKind);

        // Verify details can be deserialized to Dictionary<string, string>
        var detailsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(details.GetRawText());
        Assert.NotNull(detailsDict);
        Assert.NotEmpty(detailsDict!);
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
        var editor = await CreateTestUserAsync("pdf-val-editor-1", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync($"Validation-Test-Editor-{TestRunId}");

        // When: Editor uploads valid PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\ncontent"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "editor-rules.pdf");
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
}
