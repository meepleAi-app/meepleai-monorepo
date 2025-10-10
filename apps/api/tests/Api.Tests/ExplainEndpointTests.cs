using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for /agents/explain endpoint (AI-02).
///
/// Feature: AI-02 - RAG Explain (10 minuti)
/// As a user
/// I want to get structured explanations about game rules topics
/// So that I can understand complex concepts with proper citations
/// </summary>
public class ExplainEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ExplainEndpointTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Successful explain request with indexed content
    ///   Given an authenticated user
    ///   And a game "tic-tac-toe" with indexed PDF content
    ///   When the user requests an explanation for topic "winning conditions"
    ///   Then the system returns HTTP 200
    ///   And the response contains outline, script, citations, and estimated reading time
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WhenAuthenticated_ReturnsExplanation()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("explain-user-1", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with indexed content
        var game = await CreateTestGameAsync($"Tic-Tac-Toe-{TestRunId}");
        await SeedIndexedContentAsync(game.Id, user.Id);

        // When: User requests an explanation
        var request = new
        {
            gameId = game.Id,
            topic = "winning conditions"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with structured explanation
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify outline structure
        Assert.True(result.TryGetProperty("outline", out var outline));
        Assert.True(outline.TryGetProperty("mainTopic", out var mainTopic));
        Assert.Equal("winning conditions", mainTopic.GetString());
        Assert.True(outline.TryGetProperty("sections", out var sections));
        Assert.True(sections.GetArrayLength() > 0);

        // Verify script
        Assert.True(result.TryGetProperty("script", out var script));
        var scriptText = script.GetString();
        Assert.False(string.IsNullOrWhiteSpace(scriptText));
        Assert.Contains("winning conditions", scriptText, StringComparison.OrdinalIgnoreCase);

        // Verify citations
        Assert.True(result.TryGetProperty("citations", out var citations));
        Assert.True(citations.GetArrayLength() > 0);

        var firstCitation = citations[0];
        Assert.True(firstCitation.TryGetProperty("text", out _));
        Assert.True(firstCitation.TryGetProperty("source", out _));
        Assert.True(firstCitation.TryGetProperty("page", out var page));
        Assert.True(page.GetInt32() > 0);

        // Verify estimated reading time
        Assert.True(result.TryGetProperty("estimatedReadingTimeMinutes", out var estimatedTime));
        Assert.True(estimatedTime.GetInt32() > 0);

        // Verify optional fields
        Assert.True(result.TryGetProperty("confidence", out _));
    }

    /// <summary>
    /// Scenario: Explain without authentication
    ///   Given a user is NOT authenticated
    ///   When the user attempts to request an explanation
    ///   Then the system returns HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        var client = CreateClientWithoutCookies();

        // When: User attempts to request an explanation
        var request = new
        {
            gameId = "tic-tac-toe",
            topic = "winning conditions"
        };

        var response = await client.PostAsJsonAsync("/agents/explain", request);

        // Then: HTTP 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Explain with empty topic
    ///   Given an authenticated user
    ///   When the user requests an explanation with an empty topic
    ///   Then the system returns HTTP 200
    ///   And the script contains an error message
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WithEmptyTopic_ReturnsErrorMessage()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("explain-user-2", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests an explanation with empty topic
        var request = new
        {
            gameId = "tic-tac-toe",
            topic = ""
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with error message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("script", out var script));
        Assert.Contains("Please provide a topic", script.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Explain for game without indexed content
    ///   Given an authenticated user
    ///   And a game without indexed PDF content
    ///   When the user requests an explanation
    ///   Then the system returns HTTP 200
    ///   And the script indicates no relevant information found
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WithoutIndexedContent_ReturnsNoResults()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("explain-user-3", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game without indexed content
        var game = await CreateTestGameAsync($"Empty-Game-{TestRunId}");

        // When: User requests an explanation
        var request = new
        {
            gameId = game.Id,
            topic = "rules"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with no results message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("script", out var script));
        Assert.Contains("No relevant information found", script.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Explain with missing gameId
    ///   Given an authenticated user
    ///   When the user requests an explanation without gameId
    ///   Then the system returns HTTP 400 Bad Request
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WithoutGameId_ReturnsBadRequest()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("explain-user-4", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests an explanation without gameId
        var request = new
        {
            topic = "winning conditions"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("gameId is required", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Explain with chatId persists conversation (UI-01 integration)
    ///   Given an authenticated user with an active chat
    ///   When the user requests an explanation with chatId
    ///   Then the system persists the message to the chat
    ///   And returns the explanation
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_WithChatId_PersistsToChat()
    {
        // Given: An authenticated user with a chat
        var user = await CreateTestUserAsync("explain-user-5", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        var game = await CreateTestGameAsync($"Chat-Game-{TestRunId}");
        await SeedIndexedContentAsync(game.Id, user.Id);

        var chat = await CreateTestChatForExplainAsync(user.Id, game.Id);

        // When: User requests an explanation with chatId
        var request = new
        {
            gameId = game.Id,
            topic = "game setup",
            chatId = chat.Id
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with explanation
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Messages are persisted in chat
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var chatLogs = await db.ChatLogs
            .Where(l => l.ChatId == chat.Id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        Assert.True(chatLogs.Count >= 2); // User message + assistant response

        var userMessage = chatLogs.First(l => l.Level == "user");
        Assert.Contains("game setup", userMessage.Message);

        var assistantMessage = chatLogs.First(l => l.Level == "assistant");
        Assert.NotNull(assistantMessage.Message);
    }

    /// <summary>
    /// Scenario: Token usage is tracked (ADM-01)
    ///   Given an authenticated user
    ///   And a game with indexed content
    ///   When the user requests an explanation
    ///   Then the system logs the AI request with token counts
    /// </summary>
    [Fact]
    public async Task PostAgentsExplain_TracksTokenUsage()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("explain-user-6", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with indexed content
        var game = await CreateTestGameAsync($"Token-Game-{TestRunId}");
        await SeedIndexedContentAsync(game.Id, user.Id);

        // When: User requests an explanation
        var request = new
        {
            gameId = game.Id,
            topic = "scoring"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: AI request is logged
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var aiLog = await db.AiRequestLogs
            .Where(l => l.UserId == user.Id && l.Endpoint == "explain")
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(aiLog);
        Assert.Equal(game.Id, aiLog.GameId);
        Assert.Equal("scoring", aiLog.Query);
        Assert.Equal("Success", aiLog.Status);
        Assert.True(aiLog.LatencyMs > 0);
    }

    /// <summary>
    /// Helper: Seed indexed content for testing
    /// </summary>
    private async Task SeedIndexedContentAsync(string gameId, string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create a PDF document with extracted text
        var pdf = new PdfDocumentEntity
        {
            Id = $"pdf-explain-{gameId}-{TestRunId}",
            GameId = gameId,
            FileName = "rules.pdf",
            FilePath = "/test/rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "completed",
            ExtractedText = @"Winning Conditions: A player wins when they get three marks in a row (horizontal, vertical, or diagonal).
                             If all nine squares are filled and no player has three in a row, the game is a draw.
                             Game Setup: Place the game board in the center of the playing area. Decide which player goes first.
                             Scoring: The winner scores 1 point. In case of a draw, no points are awarded.",
            PageCount = 1,
            CharacterCount = 300,
            ProcessedAt = DateTime.UtcNow
        };

        db.PdfDocuments.Add(pdf);

        // Create vector document entity (simulating successful indexing)
        var vectorDoc = new VectorDocumentEntity
        {
            Id = $"vec-{pdf.Id}",
            GameId = gameId,
            PdfDocumentId = pdf.Id,
            ChunkCount = 3,
            TotalCharacters = 300,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "openai/text-embedding-3-small",
            EmbeddingDimensions = 1536
        };

        db.Set<VectorDocumentEntity>().Add(vectorDoc);
        await db.SaveChangesAsync();

        TrackPdfDocumentId(pdf.Id);

        // Actually index the PDF content in Qdrant (the database entities alone are not enough!)
        var indexingService = scope.ServiceProvider.GetRequiredService<PdfIndexingService>();
        await indexingService.IndexPdfAsync(pdf.Id, default);
    }

    /// <summary>
    /// Helper: Create a test chat for UI-01 integration
    /// </summary>
    private async Task<ChatEntity> CreateTestChatForExplainAsync(string userId, string gameId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Get or create agent
        var agent = await db.Agents
            .FirstOrDefaultAsync(a => a.GameId == gameId && a.Kind == "explain");

        if (agent == null)
        {
            agent = new AgentEntity
            {
                Id = $"agent-explain-{gameId}-{TestRunId}",
                GameId = gameId,
                Name = "Explain Agent",
                Kind = "explain",
                CreatedAt = DateTime.UtcNow
            };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
        }

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        return chat;
    }
}
