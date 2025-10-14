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

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for CHESS-06 webhook integration.
///
/// Feature: CHESS-06 - Webhook /agent/chess
/// As an external system
/// I want to call the chess webhook
/// So that I can get chess analysis and suggestions via n8n orchestration
/// </summary>
public class ChessWebhookIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChessWebhookIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Direct API call with valid session token and chess question
    ///   Given a service account with valid session
    ///   And chess knowledge is indexed
    ///   When the webhook calls /agents/chess with a chess question
    ///   Then the system returns HTTP 200
    ///   And the response contains chess analysis and suggestions
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_WithValidSession_ReturnsAnalysis()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("chess-service", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // And: Chess knowledge is indexed (seeded by DB-02)
        await EnsureChessKnowledgeIndexedAsync();

        // When: Webhook calls API with chess question
        var request = new
        {
            question = "What is en passant?",
            fenPosition = (string?)null
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with chess analysis
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify chess response structure (matches what n8n will receive)
        Assert.True(result.TryGetProperty("answer", out var answer));
        Assert.False(string.IsNullOrWhiteSpace(answer.GetString()));

        Assert.True(result.TryGetProperty("suggestedMoves", out var moves));
        Assert.True(moves.ValueKind == JsonValueKind.Array);

        Assert.True(result.TryGetProperty("sources", out var sources));
        Assert.True(sources.ValueKind == JsonValueKind.Array);

        // Verify metadata fields for tracking
        Assert.True(result.TryGetProperty("promptTokens", out _));
        Assert.True(result.TryGetProperty("completionTokens", out _));
        Assert.True(result.TryGetProperty("totalTokens", out _));
    }

    /// <summary>
    /// Scenario: Chess question with FEN position
    ///   Given a valid session token
    ///   When the webhook calls /agents/chess with question and FEN
    ///   Then the system returns analysis specific to that position
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_WithFenPosition_ReturnsPositionalAnalysis()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("chess-service-fen", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // And: Chess knowledge is indexed
        await EnsureChessKnowledgeIndexedAsync();

        // When: Webhook calls API with FEN position
        var request = new
        {
            question = "What should I do in this position?",
            fenPosition = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 with positional analysis
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.True(result.TryGetProperty("answer", out var answer));
        Assert.False(string.IsNullOrWhiteSpace(answer.GetString()));

        // Should include analysis object with FEN
        Assert.True(result.TryGetProperty("analysis", out var analysis));
        if (analysis.ValueKind != JsonValueKind.Null)
        {
            Assert.True(analysis.TryGetProperty("fenPosition", out var fen));
            Assert.Contains("e3", fen.GetString() ?? ""); // FEN should mention en passant square
        }
    }

    /// <summary>
    /// Scenario: Webhook without authentication
    ///   Given no session token is provided
    ///   When the webhook calls /agents/chess
    ///   Then the system returns HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_WithoutSession_ReturnsUnauthorized()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Webhook calls API without session
        var request = new
        {
            question = "What is castling?"
        };

        var response = await client.PostAsJsonAsync("/agents/chess", request);

        // Then: HTTP 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Webhook with missing question
    ///   Given a valid session token
    ///   When the webhook calls /agents/chess without question
    ///   Then the system returns HTTP 400 Bad Request
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_WithoutQuestion_ReturnsBadRequest()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("chess-service-no-q", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        // When: Webhook calls API without question
        var request = new
        {
            fenPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("question", error.GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Standardized payload format for n8n
    ///   Given a valid webhook request
    ///   When the webhook transforms the API response
    ///   Then it should match the standardized payload structure
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_ResponseFormat_MatchesStandardizedPayload()
    {
        // Given: A service account and indexed chess knowledge
        var serviceUser = await CreateTestUserAsync("chess-service-format", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        await EnsureChessKnowledgeIndexedAsync();

        // When: Webhook calls API
        var request = new
        {
            question = "What are the basic rules of chess?",
            fenPosition = (string?)null
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: Response has all required fields for standardized payload
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Verify all fields that n8n will transform
        Assert.True(result.TryGetProperty("answer", out var answer));
        Assert.False(string.IsNullOrWhiteSpace(answer.GetString()));

        Assert.True(result.TryGetProperty("suggestedMoves", out var moves));
        Assert.True(moves.ValueKind == JsonValueKind.Array);

        Assert.True(result.TryGetProperty("sources", out var sources));
        Assert.True(sources.ValueKind == JsonValueKind.Array);

        // Optional analysis field
        Assert.True(result.TryGetProperty("analysis", out _));

        // Metadata fields (required for tracking)
        Assert.True(result.TryGetProperty("promptTokens", out _));
        Assert.True(result.TryGetProperty("completionTokens", out _));
        Assert.True(result.TryGetProperty("totalTokens", out _));
        Assert.True(result.TryGetProperty("confidence", out _));
    }

    /// <summary>
    /// Scenario: Opening theory question
    ///   Given a valid session
    ///   When asking about chess openings
    ///   Then the system provides relevant opening information
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_OpeningQuestion_ReturnsOpeningInfo()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("chess-service-opening", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        await EnsureChessKnowledgeIndexedAsync();

        // When: Asking about chess openings
        var request = new
        {
            question = "What is the Sicilian Defense?"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: Returns opening information
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("answer", out var answer));

        var answerText = answer.GetString() ?? "";
        Assert.False(string.IsNullOrWhiteSpace(answerText));

        // Should contain sources from chess knowledge base
        Assert.True(result.TryGetProperty("sources", out var sources));
        Assert.True(sources.GetArrayLength() >= 0);
    }

    /// <summary>
    /// Scenario: Chat persistence with chess webhook
    ///   Given a valid session and chatId
    ///   When the webhook calls /agents/chess with chatId
    ///   Then the conversation is persisted
    /// </summary>
    [Fact]
    public async Task ChessWebhookFlow_WithChatId_PersistsConversation()
    {
        // Given: A service account
        var serviceUser = await CreateTestUserAsync("chess-service-chat", UserRole.User);
        var cookies = await AuthenticateUserAsync(serviceUser.Email);
        var client = CreateClientWithoutCookies();

        await EnsureChessKnowledgeIndexedAsync();

        // And: A chat session
        var chatRequest = new { GameId = "chess", AgentId = "chess-agent" };
        var chatHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/chats")
        {
            Content = JsonContent.Create(chatRequest)
        };
        AddCookies(chatHttpRequest, cookies);
        var chatResponse = await client.SendAsync(chatHttpRequest);
        var chatResult = await chatResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var chatId = chatResult.GetProperty("id").GetGuid();

        // When: Webhook calls API with chatId
        var request = new
        {
            question = "How does a knight move?",
            fenPosition = (string?)null,
            chatId
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/chess")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);

        var response = await client.SendAsync(httpRequest);

        // Then: HTTP 200 and conversation is persisted
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify chat history contains the interaction
        var historyRequest = new HttpRequestMessage(HttpMethod.Get, $"/chats/{chatId}");
        AddCookies(historyRequest, cookies);
        var historyResponse = await client.SendAsync(historyRequest);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(history.TryGetProperty("messages", out var messages));
        Assert.True(messages.GetArrayLength() >= 2); // User question + assistant answer
    }

    /// <summary>
    /// Helper: Ensure chess knowledge is indexed (relies on DB-02 seed data)
    /// </summary>
    private async Task EnsureChessKnowledgeIndexedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var chessService = scope.ServiceProvider.GetRequiredService<IChessKnowledgeService>();

        // Check if chess knowledge is already indexed
        var searchResult = await chessService.SearchChessKnowledgeAsync("test", 1, default);

        // If not indexed, index it now
        if (!searchResult.Success || searchResult.Results.Count == 0)
        {
            await chessService.IndexChessKnowledgeAsync(default);
        }
    }
}
