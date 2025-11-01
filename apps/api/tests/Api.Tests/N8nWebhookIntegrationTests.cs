using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for N8N-01 webhook integration.
///
/// Feature: N8N-01 - Webhook /agent/explain
/// As an external system
/// I want to call the explain webhook
/// So that I can get structured game rule explanations via n8n orchestration
/// </summary>
public class N8nWebhookIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public N8nWebhookIntegrationTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Direct API call with valid session token
    ///   Given a service account with valid session
    ///   And a game with indexed PDF content
    ///   When the webhook calls /agents/explain with session cookie
    ///   Then the system returns HTTP 200
    ///   And the response contains explain data
    /// </summary>
    [Fact]
    public async Task WebhookFlow_WithValidSession_ReturnsExplanation()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("n8n-service", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with indexed content
        var game = await CreateTestGameAsync($"Webhook-Game-{TestRunId}");
        await SeedIndexedContentForWebhookAsync(game.Id, serviceUser.Id);

        // When: Webhook calls API with session cookie
        var request = new
        {
            gameId = game.Id,
            topic = "winning conditions"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with explanation
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify explain response structure (matches what n8n will receive)
        result.TryGetProperty("outline", out var outline).Should().BeTrue();
        outline.TryGetProperty("mainTopic", out _).Should().BeTrue();
        outline.TryGetProperty("sections", out _).Should().BeTrue();

        result.TryGetProperty("script", out var script).Should().BeTrue();
        string.IsNullOrWhiteSpace(script.GetString()).Should().BeFalse();

        result.TryGetProperty("citations", out var citations).Should().BeTrue();
        citations.GetArrayLength() > 0.Should().BeTrue();

        result.TryGetProperty("estimatedReadingTimeMinutes", out _).Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Webhook without authentication
    ///   Given no session token is provided
    ///   When the webhook calls /agents/explain
    ///   Then the system returns HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task WebhookFlow_WithoutSession_ReturnsUnauthorized()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Webhook calls API without session
        var request = new
        {
            gameId = "tic-tac-toe",
            topic = "rules"
        };

        var response = await client.PostAsJsonAsync("/api/v1/agents/explain", request);

        // Then: HTTP 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Webhook with missing gameId
    ///   Given a valid session token
    ///   When the webhook calls /agents/explain without gameId
    ///   Then the system returns HTTP 400 Bad Request
    /// </summary>
    [Fact]
    public async Task WebhookFlow_WithoutGameId_ReturnsBadRequest()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("n8n-service-2", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // When: Webhook calls API without gameId
        var request = new
        {
            topic = "rules"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("gameId");
    }

    /// <summary>
    /// Scenario: Webhook with game that has no indexed content
    ///   Given a valid session token
    ///   And a game without indexed PDF content
    ///   When the webhook calls /agents/explain
    ///   Then the system returns HTTP 200
    ///   And the script indicates no relevant information found
    /// </summary>
    [Fact]
    public async Task WebhookFlow_GameWithoutContent_ReturnsNoResults()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("n8n-service-3", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // And: A game without indexed content
        var game = await CreateTestGameAsync($"Empty-Webhook-Game-{TestRunId}");

        // When: Webhook calls API
        var request = new
        {
            gameId = game.Id,
            topic = "rules"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with no results message
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("script", out var script).Should().BeTrue();
        script.GetString().Should().Contain("No relevant information found");
    }

    /// <summary>
    /// Scenario: Standardized payload format for n8n
    ///   Given a valid webhook request
    ///   When the webhook transforms the API response
    ///   Then it should match the standardized payload structure
    /// </summary>
    [Fact]
    public async Task WebhookFlow_ResponseFormat_MatchesStandardizedPayload()
    {
        // Given: A service account and game with content
        var serviceUser = await CreateTestUserAsync("n8n-service-4", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        var game = await CreateTestGameAsync($"Standard-Game-{TestRunId}");
        await SeedIndexedContentForWebhookAsync(game.Id, serviceUser.Id);

        // When: Webhook calls API
        var request = new
        {
            gameId = game.Id,
            topic = "setup"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: Response has all required fields for standardized payload
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify all fields that n8n will transform
        result.TryGetProperty("outline", out var outline).Should().BeTrue();
        outline.TryGetProperty("mainTopic", out var mainTopic).Should().BeTrue();
        mainTopic.GetString().Should().Be("setup");
        outline.TryGetProperty("sections", out var sections).Should().BeTrue();
        sections.GetArrayLength() >= 0.Should().BeTrue();

        result.TryGetProperty("script", out _).Should().BeTrue();
        result.TryGetProperty("citations", out _).Should().BeTrue();
        result.TryGetProperty("estimatedReadingTimeMinutes", out _).Should().BeTrue();

        // Metadata fields (optional but should be present for tracking)
        result.TryGetProperty("promptTokens", out _).Should().BeTrue();
        result.TryGetProperty("completionTokens", out _).Should().BeTrue();
        result.TryGetProperty("totalTokens", out _).Should().BeTrue();
    }

    /// <summary>
    /// Scenario: n8n configuration CRUD via admin endpoints
    ///   Given an admin user
    ///   When they create an n8n webhook configuration
    ///   Then the configuration is stored and retrievable
    /// </summary>
    [Fact]
    public async Task N8nConfig_CreateAndRetrieve_Success()
    {
        // Given: An admin user
        var admin = await CreateTestUserAsync("webhook-admin", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin creates n8n configuration
        var createRequest = new
        {
            name = $"Test Webhook Config {TestRunId}",
            baseUrl = "http://n8n:5678",
            apiKey = "test-api-key",
            webhookUrl = "http://n8n:5678/webhook/explain"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/n8n")
        {
            Content = JsonContent.Create(createRequest)
        };
        AddCookies(httpRequest, cookies);

        var createResponse = await client.SendAsync(httpRequest);

        // Then: Configuration is created
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        config.TryGetProperty("id", out var configId).Should().BeTrue();
        config.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be(createRequest.name);

        // And: Configuration is retrievable
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/n8n/{configId.GetString()}");
        AddCookies(getRequest, cookies);

        var getResponse = await client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedConfig = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        retrievedConfig.TryGetProperty("webhookUrl", out var webhookUrl).Should().BeTrue();
        webhookUrl.GetString().Should().Be(createRequest.webhookUrl);
    }

    /// <summary>
    /// Helper: Seed indexed content for webhook testing
    /// </summary>
    private async Task SeedIndexedContentForWebhookAsync(string gameId, string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create a PDF document with game rules
        var pdf = new PdfDocumentEntity
        {
            Id = $"pdf-webhook-{gameId}-{TestRunId}",
            GameId = gameId,
            FileName = "rules.pdf",
            FilePath = "/test/rules.pdf",
            FileSizeBytes = 4096,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "completed",
            ExtractedText = @"
                Game Setup: Place the board in the center. Each player chooses a marker (X or O).

                Winning Conditions: A player wins by placing three of their marks in a horizontal,
                vertical, or diagonal row. If all nine squares are filled and no player has won,
                the game is a draw.

                Game Rules: Players take turns placing their mark in an empty square. The player
                with X always goes first. Once a square is taken, it cannot be changed.
            ",
            PageCount = 1,
            CharacterCount = 450,
            ProcessedAt = DateTime.UtcNow
        };

        db.PdfDocuments.Add(pdf);

        // Create vector document entity
        var vectorDoc = new VectorDocumentEntity
        {
            Id = $"vec-{pdf.Id}",
            GameId = gameId,
            PdfDocumentId = pdf.Id,
            ChunkCount = 3,
            TotalCharacters = 450,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "openai/text-embedding-3-small",
            EmbeddingDimensions = 1536
        };

        db.Set<VectorDocumentEntity>().Add(vectorDoc);
        await db.SaveChangesAsync();

        TrackPdfDocumentId(pdf.Id);

        // Index the PDF content in Qdrant
        var indexingService = scope.ServiceProvider.GetRequiredService<PdfIndexingService>();
        await indexingService.IndexPdfAsync(pdf.Id, default);
    }
}
