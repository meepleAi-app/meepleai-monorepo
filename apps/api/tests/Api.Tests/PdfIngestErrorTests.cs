using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Comprehensive error case tests for PDF Ingest endpoints (/ingest/pdf, /ingest/pdf/{pdfId}/index, /ingest/pdf/{pdfId}/rulespec).
/// Tests all possible error scenarios (400, 401, 403, 404).
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage.
/// </summary>
public class PdfIngestErrorTests : IntegrationTestBase
{
    public PdfIngestErrorTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region POST /ingest/pdf Error Cases

    [Fact]
    public async Task PostIngestPdf_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test-game"), "gameId");

        // When: User tries to upload PDF without authentication
        var response = await client.PostAsync("/ingest/pdf", content);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIngestPdf_WithUserRole_ReturnsForbidden()
    {
        // Given: Regular user (not Admin/Editor)
        var user = await CreateTestUserAsync("regular-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test-game"), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf");
        request.Content = content;
        AddCookies(request, cookies);

        // When: Regular user tries to upload PDF
        var response = await client.SendAsync(request);

        // Then: System returns forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostIngestPdf_WithMissingGameId_ReturnsBadRequest()
    {
        // Given: Editor user without gameId but with file
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();

        var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        // Add a dummy file to make the form valid
        var fileContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");
        // Missing gameId field

        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf");
        request.Content = content;
        AddCookies(request, cookies);

        // When: Editor tries to upload PDF without gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostIngestPdf_WithEmptyGameId_ReturnsBadRequest()
    {
        // Given: Editor user with empty gameId
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf");
        request.Content = content;
        AddCookies(request, cookies);

        // When: Editor tries to upload PDF with empty gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostIngestPdf_WithMissingFile_ReturnsBadRequest()
    {
        // Given: Editor user without file
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test-game"), "gameId");
        // Missing file

        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf");
        request.Content = content;
        AddCookies(request, cookies);

        // When: Editor tries to upload without file
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST /ingest/pdf/{pdfId}/index Error Cases

    [Fact]
    public async Task PostIndexPdf_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();

        // When: User tries to index PDF without authentication
        var response = await client.PostAsync("/ingest/pdf/test-pdf-id/index", null);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIndexPdf_WithUserRole_ReturnsForbidden()
    {
        // Given: Regular user (not Admin/Editor)
        var user = await CreateTestUserAsync("regular-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf/test-pdf-id/index");
        AddCookies(request, cookies);

        // When: Regular user tries to index PDF
        var response = await client.SendAsync(request);

        // Then: System returns forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostIndexPdf_WithNonExistentPdfId_ReturnsNotFound()
    {
        // Given: Editor user with non-existent PDF ID
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf/non-existent-pdf/index");
        AddCookies(request, cookies);

        // When: Editor tries to index non-existent PDF
        var response = await client.SendAsync(request);

        // Then: System returns not found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostIndexPdf_WithoutExtractedText_ReturnsBadRequest()
    {
        // Given: Editor user and PDF without extracted text
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var game = await CreateTestGameAsync("Test Game");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, editor.Id);
        // PDF created without ExtractedText
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/pdf/{pdf.Id}/index");
        AddCookies(request, cookies);

        // When: Editor tries to index PDF without extracted text
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST /ingest/pdf/{pdfId}/rulespec Error Cases

    [Fact]
    public async Task PostGenerateRuleSpec_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();

        // When: User tries to generate RuleSpec without authentication
        var response = await client.PostAsync("/ingest/pdf/test-pdf-id/rulespec", null);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostGenerateRuleSpec_WithUserRole_ReturnsForbidden()
    {
        // Given: Regular user (not Admin/Editor)
        var user = await CreateTestUserAsync("regular-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf/test-pdf-id/rulespec");
        AddCookies(request, cookies);

        // When: Regular user tries to generate RuleSpec
        var response = await client.SendAsync(request);

        // Then: System returns forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostGenerateRuleSpec_WithNonExistentPdfId_ReturnsBadRequest()
    {
        // Given: Editor user with non-existent PDF ID
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/pdf/non-existent-pdf/rulespec");
        AddCookies(request, cookies);

        // When: Editor tries to generate RuleSpec for non-existent PDF
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region GET /pdfs/{pdfId}/text Error Cases

    [Fact]
    public async Task GetPdfText_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();

        // When: User tries to get PDF text without authentication
        var response = await client.GetAsync("/pdfs/test-pdf-id/text");

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPdfText_WithNonExistentPdfId_ReturnsNotFound()
    {
        // Given: Authenticated user with non-existent PDF ID
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/pdfs/non-existent-pdf/text");
        AddCookies(request, cookies);

        // When: User tries to get text for non-existent PDF
        var response = await client.SendAsync(request);

        // Then: System returns not found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
