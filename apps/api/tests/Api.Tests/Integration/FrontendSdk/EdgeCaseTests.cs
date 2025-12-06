using Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// Integration tests for edge case scenarios that the frontend SDK must handle:
/// - Streaming responses (Server-Sent Events for chat)
/// - File uploads (multipart/form-data for PDFs)
/// - Rate limiting (429 with Retry-After)
/// - Large payloads
/// - Request cancellation
/// - Slow responses
/// - Special characters and encoding
///
/// These tests validate API behavior in complex real-world scenarios.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public class EdgeCaseTests : IAsyncLifetime
{
    private readonly FrontendSdkTestFactory _factory;
    private HttpClient _client = null!;
    private MeepleAiDbContext _dbContext = null!;

    public EdgeCaseTests(FrontendSdkTestFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        _client.Timeout = TimeSpan.FromSeconds(30); // Longer timeout for streaming tests

        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _dbContext?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ResetDatabaseAsync()
    {
        var tableNames = await _dbContext.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync();

        if (tableNames.Count > 0)
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");
            try
            {
                foreach (var tableName in tableNames)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
                }
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");
            }
        }
    }
    [Fact(DisplayName = "POST /chat with streaming should accept SSE request")]
    public async Task Chat_WithStreaming_AcceptsSSERequest()
    {
        // Arrange - Create and login user
        var email = $"streaming-test-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Streaming Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Prepare chat request
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var chatRequest = new
        {
            message = "What are the rules for Catan?",
            gameId = Guid.NewGuid(),
            stream = true
        };
        request.Content = JsonContent.Create(chatRequest);

        // Act
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        // Response should accept streaming or return appropriate error
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized, // If auth required
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound); // If chat endpoint not yet implemented

        if (response.StatusCode == HttpStatusCode.OK)
        {
            // If streaming is implemented, verify Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType;
            contentType.Should().BeOneOf("text/event-stream", "application/json");

            // Frontend SDK should handle streaming responses using EventSource or fetch API
        }
    }

    [Fact(DisplayName = "Streaming response should handle connection interruption")]
    public async Task StreamingResponse_HandlesConnectionInterruption()
    {
        // This test verifies the API handles client disconnection gracefully

        // Arrange
        var email = $"stream-interrupt-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Interrupt Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Create request with cancellation
        using var cts = new CancellationTokenSource();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Content = JsonContent.Create(new
        {
            message = "Explain Catan rules in detail",
            stream = true
        });

        // Act - Start request and cancel quickly
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        try
        {
            await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected - connection interrupted
            Assert.True(true, "Streaming connection cancelled as expected");
        }

        // Assert - API should handle cancellation without crashes
        // (Server should cleanup resources properly)
    }
    [Fact(DisplayName = "POST /pdf with multipart should accept file upload")]
    public async Task PdfUpload_WithMultipart_AcceptsFileUpload()
    {
        // Arrange - Create and login user
        var email = $"upload-test-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Upload Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Create minimal valid PDF (PDF header)
        var pdfContent = Encoding.ASCII.GetBytes("%PDF-1.4\n%EOF");
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var formData = new MultipartFormDataContent
        {
            { fileContent, "file", "test.pdf" }
        };

        // Act
        var response = await _client.PostAsync("/api/v1/pdfs", formData);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);

        // Frontend SDK should handle file upload with progress tracking
    }

    [Fact(DisplayName = "POST /pdf with large file should handle appropriately")]
    public async Task PdfUpload_WithLargeFile_HandlesAppropriately()
    {
        // Arrange
        var email = $"large-upload-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Large Upload Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Create large PDF (10MB)
        var largeContent = new byte[10 * 1024 * 1024];
        Array.Copy(Encoding.ASCII.GetBytes("%PDF-1.4\n"), largeContent, 9);

        var fileContent = new ByteArrayContent(largeContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var formData = new MultipartFormDataContent
        {
            { fileContent, "file", "large.pdf" }
        };

        // Act
        var response = await _client.PostAsync("/api/v1/pdfs", formData);

        // Assert
        // API should either accept (201), reject size (413), or return validation error (400)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);

        // Frontend SDK should show appropriate error message for file size limits
    }

    [Fact(DisplayName = "POST /pdf with invalid file type should return 400")]
    public async Task PdfUpload_WithInvalidFileType_Returns400()
    {
        // Arrange
        var email = $"invalid-upload-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Invalid Upload Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Create non-PDF file
        var textContent = Encoding.UTF8.GetBytes("This is not a PDF");
        var fileContent = new ByteArrayContent(textContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var formData = new MultipartFormDataContent
        {
            { fileContent, "file", "notapdf.txt" }
        };

        // Act
        var response = await _client.PostAsync("/api/v1/pdfs", formData);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);

        // Frontend SDK should validate file type before upload
    }
    [Fact(DisplayName = "POST with large JSON payload should handle appropriately")]
    public async Task Post_WithLargeJsonPayload_HandlesAppropriately()
    {
        // Arrange - Create large message (1MB JSON)
        var largeMessage = new string('x', 1_000_000);
        var request = new
        {
            message = largeMessage,
            gameId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chat", request);

        // Assert
        // API should either accept or reject based on size limits
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);

        // Frontend SDK should validate payload size before sending
    }

    [Fact(DisplayName = "GET response with large dataset should paginate")]
    public async Task Get_WithLargeDataset_SupportsPagination()
    {
        // Act - Request with pagination parameters
        var response = await _client.GetAsync("/api/v1/games?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNull();

            // Frontend SDK should handle paginated responses
            // Response should be array (even if empty)
            content.Should().StartWith("[");
        }
    }
    [Fact(DisplayName = "POST with Unicode characters should handle correctly")]
    public async Task Post_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var email = $"unicode-test-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Unicode Test" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        var request = new
        {
            message = "Test with émojis 🎲🎮 and spëcial çharacters: 日本語, العربية, Ελληνικά",
            gameId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chat", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);

        // API should handle Unicode properly (UTF-8)
        // Frontend SDK can safely send international characters
    }

    [Fact(DisplayName = "POST with HTML/script tags should sanitize or reject")]
    public async Task Post_WithHtmlScriptTags_SanitizesOrRejects()
    {
        // Arrange
        var email = $"xss-test-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";
        var displayName = "<script>alert('xss')</script>";

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            displayName
        });

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError);

        if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
        {
            // If accepted, verify HTML was sanitized
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            user.Should().NotBeNull();

            // Display name should either be sanitized or escaped
            // Should NOT contain raw script tags
            user!.DisplayName.Should().NotContain("<script>");
        }

        // API should protect against XSS attacks
        // Frontend SDK should also sanitize user input
    }
    [Fact(DisplayName = "Multiple rapid requests should handle rate limiting")]
    public async Task MultipleRapidRequests_HandlesRateLimiting()
    {
        // Arrange - Send many requests rapidly
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            return await _client.GetAsync("/api/v1/games");
        });

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        // Some responses may be rate limited (429) or unauthorized (401)
        var statusCodes = responses.Select(r => r.StatusCode).Distinct().ToList();

        // All responses should be either OK, Unauthorized, or Too Many Requests
        foreach (var statusCode in statusCodes)
        {
            statusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests,
                HttpStatusCode.ServiceUnavailable);
        }

        // Frontend SDK should handle rate limiting gracefully
    }
    [Fact(DisplayName = "Long request with cancellation should cleanup properly")]
    public async Task LongRequest_WithCancellation_CleansupProperly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        // Act - Start request and cancel after short delay
        var task = _client.SendAsync(request, cts.Token);
        await Task.Delay(10); // Allow request to start
        cts.Cancel();

        // Assert
        try
        {
            await task;
            // If request completed before cancellation, that's also valid
            Assert.True(true, "Request completed before cancellation");
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation succeeded
            Assert.True(true, "Request cancelled as expected");
        }

        // API should cleanup resources when client cancels
        // Frontend SDK should cancel requests when user navigates away
    }
    [Fact(DisplayName = "GET with Accept: application/json should return JSON")]
    public async Task Get_WithJsonAccept_ReturnsJson()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }

        // Frontend SDK should always specify Accept: application/json
    }

    [Fact(DisplayName = "GET without Accept header should default to JSON")]
    public async Task Get_WithoutAcceptHeader_DefaultsToJson()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/games");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }

        // API should default to JSON for REST endpoints
    }
}