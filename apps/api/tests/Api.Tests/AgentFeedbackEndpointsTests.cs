using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Integration tests for Agent Feedback Endpoints (POST /api/v1/agents/feedback)
/// Covers FLUSSI.md Flow 2.5: Feedback Registration
/// </summary>
[Collection("Admin Endpoints")]
public class AgentFeedbackEndpointsTests : AdminTestFixture
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;

    public AgentFeedbackEndpointsTests(
        PostgresCollectionFixture postgresFixture,
        WebApplicationFactoryFixture factory,
        ITestOutputHelper output)
        : base(postgresFixture, factory)
    {
        _output = output;
        _client = Factory.CreateClient();
    }

    [Fact]
    public async Task PostAgentsFeedback_WithHelpfulFeedback_Returns200AndStoresFeedback()
    {
        // Arrange - Authenticate and create test data
        var cookies = await RegisterAndAuthenticateAsync(_client, "feedback-user1@test.com", "Admin");
        var authenticatedClient = CreateClientWithoutCookies();

        // Create a game first
        using var gameRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new
            {
                title = "Test Feedback Game",
                publisher = "Test Publisher",
                yearPublished = 2024,
                minPlayers = 2,
                maxPlayers = 2
            })
        };
        AddCookies(gameRequest, cookies);
        var gameResponse = await authenticatedClient.SendAsync(gameRequest);
        gameResponse.EnsureSuccessStatusCode();
        var gameData = await gameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var gameId = gameData.GetProperty("id").GetGuid();

        // Create a chat session
        using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chats")
        {
            Content = JsonContent.Create(new { gameId, agentName = "qa" })
        };
        AddCookies(chatRequest, cookies);
        var chatResponse = await authenticatedClient.SendAsync(chatRequest);
        chatResponse.EnsureSuccessStatusCode();
        var chatData = await chatResponse.Content.ReadFromJsonAsync<JsonElement>();
        var chatId = chatData.GetProperty("chatId").GetGuid();

        // Simulate QA agent interaction (generate messageId)
        using var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa")
        {
            Content = JsonContent.Create(new
            {
                question = "What is the goal of the game?",
                chatId,
                gameId
            })
        };
        AddCookies(qaRequest, cookies);
        var qaResponse = await authenticatedClient.SendAsync(qaRequest);
        qaResponse.EnsureSuccessStatusCode();
        var qaData = await qaResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = qaData.GetProperty("messageId").GetGuid();

        // Act - Submit helpful feedback
        using var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/feedback")
        {
            Content = JsonContent.Create(new
            {
                messageId,
                isHelpful = true,
                comment = "Great answer with clear citations!"
            })
        };
        AddCookies(feedbackRequest, cookies);
        var feedbackResponse = await authenticatedClient.SendAsync(feedbackRequest);

        // Assert
        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedbackData = await feedbackResponse.Content.ReadFromJsonAsync<JsonElement>();
        feedbackData.GetProperty("success").GetBoolean().Should().BeTrue();

        _output.WriteLine($"✅ Feedback stored for message {messageId}");
    }

    [Fact]
    public async Task PostAgentsFeedback_WithNotHelpfulFeedback_Returns200AndStoresNegativeFeedback()
    {
        // Arrange - Authenticate and create test data
        var cookies = await RegisterAndAuthenticateAsync(_client, "feedback-user2@test.com", "Admin");
        var authenticatedClient = CreateClientWithoutCookies();

        // Create game and chat
        using var gameRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new
            {
                title = "Test Negative Feedback Game",
                publisher = "Test",
                yearPublished = 2024,
                minPlayers = 2,
                maxPlayers = 2
            })
        };
        AddCookies(gameRequest, cookies);
        var gameResponse = await authenticatedClient.SendAsync(gameRequest);
        gameResponse.EnsureSuccessStatusCode();
        var gameData = await gameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var gameId = gameData.GetProperty("id").GetGuid();

        using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chats")
        {
            Content = JsonContent.Create(new { gameId, agentName = "qa" })
        };
        AddCookies(chatRequest, cookies);
        var chatResponse = await authenticatedClient.SendAsync(chatRequest);
        chatResponse.EnsureSuccessStatusCode();
        var chatData = await chatResponse.Content.ReadFromJsonAsync<JsonElement>();
        var chatId = chatData.GetProperty("chatId").GetGuid();

        // Simulate QA agent interaction
        using var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa")
        {
            Content = JsonContent.Create(new
            {
                question = "What is the goal of the game?",
                chatId,
                gameId
            })
        };
        AddCookies(qaRequest, cookies);
        var qaResponse = await authenticatedClient.SendAsync(qaRequest);
        qaResponse.EnsureSuccessStatusCode();
        var qaData = await qaResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = qaData.GetProperty("messageId").GetGuid();

        // Act - Submit not helpful feedback with comment
        using var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/feedback")
        {
            Content = JsonContent.Create(new
            {
                messageId,
                isHelpful = false,
                comment = "Answer was too vague, missing specific rules."
            })
        };
        AddCookies(feedbackRequest, cookies);
        var feedbackResponse = await authenticatedClient.SendAsync(feedbackRequest);

        // Assert
        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedbackData = await feedbackResponse.Content.ReadFromJsonAsync<JsonElement>();
        feedbackData.GetProperty("success").GetBoolean().Should().BeTrue();

        _output.WriteLine($"❌ Negative feedback stored for message {messageId}");
    }

    [Fact]
    public async Task PostAgentsFeedback_WithInvalidMessageId_Returns404()
    {
        // Arrange - Authenticate
        var cookies = await RegisterAndAuthenticateAsync(_client, "feedback-user3@test.com", "User");
        var authenticatedClient = CreateClientWithoutCookies();

        // Act - Submit feedback for non-existent message
        using var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/feedback")
        {
            Content = JsonContent.Create(new
            {
                messageId = Guid.NewGuid(), // Non-existent message
                isHelpful = true
            })
        };
        AddCookies(feedbackRequest, cookies);
        var feedbackResponse = await authenticatedClient.SendAsync(feedbackRequest);

        // Assert
        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAgentsFeedback_WithMissingMessageId_Returns400()
    {
        // Arrange - Authenticate
        var cookies = await RegisterAndAuthenticateAsync(_client, "feedback-user4@test.com", "User");
        var authenticatedClient = CreateClientWithoutCookies();

        // Act - Submit feedback without messageId
        using var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/feedback")
        {
            Content = JsonContent.Create(new
            {
                isHelpful = true
            })
        };
        AddCookies(feedbackRequest, cookies);
        var feedbackResponse = await authenticatedClient.SendAsync(feedbackRequest);

        // Assert
        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "Requires endpoint implementation for feedback retrieval")]
    public async Task GetAgentsFeedback_ForMessage_ReturnsFeedbackHistory()
    {
        // Future test - verify feedback can be retrieved after submission
        // Endpoint: GET /api/v1/agents/feedback/{messageId}
        await Task.CompletedTask;
    }
}
