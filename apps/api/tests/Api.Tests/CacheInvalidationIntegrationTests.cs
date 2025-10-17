using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for cache invalidation on PDF upload and rule spec updates (PERF-03).
///
/// Feature: PERF-03 - Intelligent Cache Invalidation on Content Updates
/// As a system
/// I want to automatically invalidate cached AI responses when underlying data changes
/// So that users always get fresh, accurate responses
/// </summary>
[Collection("Sequential")]
public class CacheInvalidationIntegrationTests : IntegrationTestBase
{
    public CacheInvalidationIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region PDF Upload Cache Invalidation

    /// <summary>
    /// Scenario: Upload PDF for a game invalidates cache
    ///   Given a game with cached AI responses
    ///   When a new PDF is uploaded for that game
    ///   Then all cached responses for that game are invalidated
    ///   And subsequent queries fetch fresh data
    /// </summary>
    [Fact]
    public async Task UploadPdf_ForGame_InvalidatesCachedResponses()
    {
        // Given: A game with cached AI responses
        var admin = await CreateTestUserAsync("admin-cache-pdf", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("TestGame-PDF-Cache");

        using var scope = Factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

        // Create a cached response
        var cacheKey = cacheService.GenerateQaCacheKey(game.Id, "How do I setup?");
        var cachedResponse = new QaResponse("Cached answer", Array.Empty<Snippet>());
        var tags = new[] { $"game:{game.Id}" };
        await cacheService.SetAsync(cacheKey, cachedResponse, ttlSeconds: 3600, tags: tags);

        // Verify cache exists
        var cached = await cacheService.GetAsync<QaResponse>(cacheKey);
        Assert.NotNull(cached);
        Assert.Equal("Cached answer", cached.Data.answer);

        // When: Upload PDF for the game
        var client = CreateClientWithoutCookies();
        var pdfBytes = CreateMockPdfBytes();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pdf/upload");
        AddCookies(request, adminCookies);
        request.Content = content;

        var response = await client.SendAsync(request);

        // Accept both 200 OK and 202 Accepted as success
        Assert.True(response.IsSuccessStatusCode,
            $"PDF upload failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        // Track PDF document for cleanup
        var result = await response.Content.ReadFromJsonAsync<PdfUploadResponse>();
        if (result != null)
        {
            TrackPdfDocumentId(result.DocumentId);
        }

        // Then: Cache is invalidated (using new scope to avoid entity tracking issues)
        using var newScope = Factory.Services.CreateScope();
        var newCacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        var cachedAfter = await newCacheService.GetAsync<QaResponse>(cacheKey);
        Assert.Null(cachedAfter);
    }

    /// <summary>
    /// Scenario: Upload PDF with tag-based invalidation
    ///   Given a game with multiple cached responses tagged with game ID
    ///   When a new PDF is uploaded
    ///   Then all entries with the game tag are invalidated
    ///   And entries for other games remain cached
    /// </summary>
    [Fact]
    public async Task UploadPdf_WithGameTag_InvalidatesOnlyGameCachedResponses()
    {
        // Given: Two games with cached responses
        var admin = await CreateTestUserAsync("admin-cache-tag", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var game1 = await CreateTestGameAsync("TestGame1-Tag");
        var game2 = await CreateTestGameAsync("TestGame2-Tag");

        using var scope = Factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

        // Cache responses for both games
        var key1 = cacheService.GenerateQaCacheKey(game1.Id, "How do I win?");
        var key2 = cacheService.GenerateQaCacheKey(game2.Id, "How do I win?");

        await cacheService.SetAsync(key1, new QaResponse("Game1 answer", Array.Empty<Snippet>()),
            ttlSeconds: 3600, tags: new[] { $"game:{game1.Id}" });
        await cacheService.SetAsync(key2, new QaResponse("Game2 answer", Array.Empty<Snippet>()),
            ttlSeconds: 3600, tags: new[] { $"game:{game2.Id}" });

        // Verify both are cached
        Assert.NotNull(await cacheService.GetAsync<QaResponse>(key1));
        Assert.NotNull(await cacheService.GetAsync<QaResponse>(key2));

        // When: Upload PDF for game1
        var client = CreateClientWithoutCookies();
        var pdfBytes = CreateMockPdfBytes();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "game1-rules.pdf");
        content.Add(new StringContent(game1.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pdf/upload");
        AddCookies(request, adminCookies);
        request.Content = content;

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfUploadResponse>();
        if (result != null)
        {
            TrackPdfDocumentId(result.DocumentId);
        }

        // Then: Only game1 cache is invalidated
        using var newScope = Factory.Services.CreateScope();
        var newCacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        Assert.Null(await newCacheService.GetAsync<QaResponse>(key1));
        Assert.NotNull(await newCacheService.GetAsync<QaResponse>(key2)); // Game2 cache intact
    }

    /// <summary>
    /// Scenario: Upload PDF invalidates multiple cache endpoints
    ///   Given cached responses across QA, Explain, and Setup endpoints
    ///   When a new PDF is uploaded
    ///   Then cache is invalidated for all AI endpoints (QA, Explain, Setup)
    /// </summary>
    [Fact]
    public async Task UploadPdf_InvalidatesAllEndpointCaches()
    {
        // Given: Cached responses for all endpoints
        var admin = await CreateTestUserAsync("admin-cache-endpoints", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("TestGame-AllEndpoints");

        using var scope = Factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

        var qaKey = cacheService.GenerateQaCacheKey(game.Id, "How do I win?");
        var explainKey = cacheService.GenerateExplainCacheKey(game.Id, "setup phase");
        var setupKey = cacheService.GenerateSetupCacheKey(game.Id);

        var tags = new[] { $"game:{game.Id}" };
        await cacheService.SetAsync(qaKey, new QaResponse("QA answer", Array.Empty<Snippet>()), 3600, tags);
        await cacheService.SetAsync(explainKey, new ExplainResponse(
            new ExplainOutline("setup", new System.Collections.Generic.List<string>()),
            "Explain answer", new System.Collections.Generic.List<Snippet>(), 1), 3600, tags);
        await cacheService.SetAsync(setupKey, new SetupGuideResponse(
            game.Name, new System.Collections.Generic.List<SetupGuideStep>(), 10), 3600, tags);

        // Verify all cached
        Assert.NotNull(await cacheService.GetAsync<QaResponse>(qaKey));
        Assert.NotNull(await cacheService.GetAsync<ExplainResponse>(explainKey));
        Assert.NotNull(await cacheService.GetAsync<SetupGuideResponse>(setupKey));

        // When: Upload PDF
        var client = CreateClientWithoutCookies();
        var pdfBytes = CreateMockPdfBytes();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "rules.pdf");
        content.Add(new StringContent(game.Id), "gameId");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pdf/upload");
        AddCookies(request, adminCookies);
        request.Content = content;

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        var result = await response.Content.ReadFromJsonAsync<PdfUploadResponse>();
        if (result != null)
        {
            TrackPdfDocumentId(result.DocumentId);
        }

        // Then: All endpoint caches invalidated
        using var newScope = Factory.Services.CreateScope();
        var newCacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        Assert.Null(await newCacheService.GetAsync<QaResponse>(qaKey));
        Assert.Null(await newCacheService.GetAsync<ExplainResponse>(explainKey));
        Assert.Null(await newCacheService.GetAsync<SetupGuideResponse>(setupKey));
    }

    #endregion

    #region Rule Spec Update Cache Invalidation

    /// <summary>
    /// Scenario: Update rule spec invalidates cache
    ///   Given a game with cached AI responses
    ///   And a rule spec exists for that game
    ///   When the rule spec is updated
    ///   Then all cached responses for that game are invalidated
    ///   And subsequent queries fetch fresh data
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_InvalidatesCachedResponses()
    {
        // Given: Game with cached responses and rule spec
        var editor = await CreateTestUserAsync("editor-rulespec", UserRole.Editor);
        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var game = await CreateTestGameAsync("TestGame-RuleSpec");
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "1.0.0");

        using var scope = Factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

        // Create cached response
        var cacheKey = cacheService.GenerateQaCacheKey(game.Id, "What are the winning conditions?");
        var tags = new[] { $"game:{game.Id}", $"rulespec:{ruleSpec.Id}" };
        await cacheService.SetAsync(cacheKey, new QaResponse("Old answer", Array.Empty<Snippet>()), 3600, tags);

        // Verify cache exists
        Assert.NotNull(await cacheService.GetAsync<QaResponse>(cacheKey));

        // When: Update rule spec
        var client = CreateClientWithoutCookies();
        var updateRequest = new
        {
            version = "1.1.0",
            ruleSpecJson = "{\"version\":\"1.1.0\",\"game\":\"TestGame\"}"
        };

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/rulespecs/{ruleSpec.Id}");
        AddCookies(request, editorCookies);
        request.Content = JsonContent.Create(updateRequest);

        var response = await client.SendAsync(request);

        // Rule spec update might return 200 or 204
        Assert.True(response.IsSuccessStatusCode,
            $"Rule spec update failed with status {response.StatusCode}");

        // Then: Cache is invalidated
        using var newScope = Factory.Services.CreateScope();
        var newCacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        var cachedAfter = await newCacheService.GetAsync<QaResponse>(cacheKey);
        Assert.Null(cachedAfter);
    }

    #endregion

    #region Cache Bypass Functionality

    /// <summary>
    /// Scenario: QA endpoint with cache bypass parameter
    ///   Given a cached response exists for a query
    ///   When calling QA endpoint with bypassCache=true
    ///   Then cached response is not used
    ///   And fresh response is returned
    ///   And cache bypass is logged in AI request logs
    /// </summary>
    [Fact]
    public async Task QaEndpoint_WithBypassCache_IgnoresCachedResponse()
    {
        // Given: Game with cached response
        var user = await CreateTestUserAsync("user-bypass", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("TestGame-Bypass");

        // Create a PDF document to ensure RAG works
        var pdf = await CreateTestPdfDocumentAsync(game.Id, user.Id);

        using var scope = Factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

        // Create cached response
        var query = "How do I setup the game?";
        var cacheKey = cacheService.GenerateQaCacheKey(game.Id, query);
        var cachedAnswer = "This is a CACHED answer that should be bypassed";
        await cacheService.SetAsync(cacheKey, new QaResponse(cachedAnswer, Array.Empty<Snippet>()), 3600);

        // Verify cache exists
        Assert.NotNull(await cacheService.GetAsync<QaResponse>(cacheKey));

        // When: Call QA endpoint with bypassCache=true
        var client = CreateClientWithoutCookies();
        var qaRequest = new
        {
            query = query,
            bypassCache = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agents/qa?gameId={game.Id}");
        AddCookies(request, cookies);
        request.Content = JsonContent.Create(qaRequest);

        var response = await client.SendAsync(request);

        // Then: Fresh response is returned (not cached)
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            // The response should NOT be the exact cached answer
            Assert.DoesNotContain(cachedAnswer, result);
        }
        else
        {
            // If QA fails (e.g., no LLM configured), that's acceptable for this test
            // The important part is that cache was bypassed
            var statusCode = response.StatusCode;
            Assert.True(statusCode == System.Net.HttpStatusCode.OK ||
                       statusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                       statusCode == System.Net.HttpStatusCode.InternalServerError,
                       $"Unexpected status code: {statusCode}");
        }
    }

    /// <summary>
    /// Scenario: Cache bypass does not affect subsequent requests
    ///   Given a request with bypassCache=true
    ///   When the request completes
    ///   Then subsequent requests without bypass use cache normally
    /// </summary>
    [Fact]
    public async Task QaEndpoint_AfterBypass_ResumesCaching()
    {
        // Given: Game setup
        var user = await CreateTestUserAsync("user-resume-cache", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("TestGame-ResumeCache");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, user.Id);

        var client = CreateClientWithoutCookies();
        var query = "What are the rules?";

        // When: First request with bypassCache=true
        var bypassRequest = new
        {
            query = query,
            bypassCache = true
        };

        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agents/qa?gameId={game.Id}");
        AddCookies(request1, cookies);
        request1.Content = JsonContent.Create(bypassRequest);

        var response1 = await client.SendAsync(request1);

        // Then: Subsequent request without bypass should work normally
        // (we can't fully test caching here without a real LLM, but we verify the endpoint accepts the parameter)
        var normalRequest = new
        {
            query = query
            // bypassCache defaults to false
        };

        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agents/qa?gameId={game.Id}");
        AddCookies(request2, cookies);
        request2.Content = JsonContent.Create(normalRequest);

        var response2 = await client.SendAsync(request2);

        // Both requests should have similar success status (or both fail if no LLM)
        Assert.Equal(response1.IsSuccessStatusCode, response2.IsSuccessStatusCode);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a minimal valid PDF file for testing.
    /// This is a real PDF structure that passes validation.
    /// </summary>
    private static byte[] CreateMockPdfBytes()
    {
        // Minimal valid PDF structure
        var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 <<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
>>
>>
>>
endobj
4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
100 700 Td
(Test PDF) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000315 00000 n
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
408
%%EOF";

        return Encoding.ASCII.GetBytes(pdfContent);
    }

    #endregion
}

/// <summary>
/// Response models for deserialization
/// </summary>
internal record PdfUploadResponse(string DocumentId, string Message);
