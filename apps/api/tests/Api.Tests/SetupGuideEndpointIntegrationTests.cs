using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for Setup Guide endpoint (AI-03).
///
/// Feature: Board Game Setup Guide Generation
/// As an authenticated user
/// I want to generate step-by-step setup guides for board games using RAG + LLM
/// So that I can prepare games quickly and correctly without reading the full rulebook
/// </summary>
public class SetupGuideEndpointIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SetupGuideEndpointIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Authenticated user generates setup guide for existing game
    ///   Given a user is authenticated
    ///   And a game exists in the database
    ///   When user requests a setup guide for that game
    ///   Then a structured setup guide is returned
    ///   And the guide contains numbered steps
    ///   And each step has title, instruction, and references
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenRequestingSetupGuide_ThenReturnsStructuredGuide()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-user-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists in the database
        var game = await CreateTestGameAsync("Test Board Game");

        // When: User requests a setup guide for that game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: A structured setup guide is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);

        // And: The guide contains numbered steps
        Assert.NotNull(setupGuide);
        Assert.NotEmpty(setupGuide!.steps);
        Assert.All(setupGuide.steps, step =>
        {
            Assert.True(step.stepNumber > 0);
            Assert.False(string.IsNullOrWhiteSpace(step.title));
            Assert.False(string.IsNullOrWhiteSpace(step.instruction));
            Assert.NotNull(step.references);
        });

        // And: Each step has title, instruction, and references
        var firstStep = setupGuide.steps[0];
        Assert.Equal(1, firstStep.stepNumber);
        Assert.NotNull(firstStep.title);
        Assert.NotNull(firstStep.instruction);
    }

    /// <summary>
    /// Scenario: Unauthenticated user attempts to generate setup guide
    ///   Given no authentication is provided
    ///   When user requests a setup guide
    ///   Then the request is rejected with Unauthorized
    /// </summary>
    [Fact]
    public async Task GivenNoAuthentication_WhenRequestingSetupGuide_ThenReturnsUnauthorized()
    {
        // Given: No authentication is provided
        var client = CreateClientWithoutCookies();

        // When: User requests a setup guide
        var response = await client.PostAsJsonAsync("/api/v1/agents/setup", new SetupGuideRequest("any-game", null));

        // Then: The request is rejected with Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User requests setup guide without game ID
    ///   Given a user is authenticated
    ///   When user requests a setup guide without providing a game ID
    ///   Then the request is rejected with BadRequest
    /// </summary>
    [Fact]
    public async Task GivenNoGameId_WhenRequestingSetupGuide_ThenReturnsBadRequest()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-badreq-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests a setup guide without providing a game ID
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest("", null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: The request is rejected with BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User requests setup guide for non-existent game
    ///   Given a user is authenticated
    ///   When user requests a setup guide for a game that doesn't exist
    ///   Then a default setup guide is returned
    ///   And the game title is "Unknown Game"
    /// </summary>
    [Fact]
    public async Task GivenNonExistentGame_WhenRequestingSetupGuide_ThenReturnsDefaultGuide()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-notfound-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests a setup guide for a game that doesn't exist
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest("nonexistent-game-id", null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: A default setup guide is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);

        // And: The game title is "Unknown Game"
        Assert.NotNull(setupGuide);
        Assert.Equal("Unknown Game", setupGuide!.gameTitle);
        Assert.NotEmpty(setupGuide.steps);
    }

    /// <summary>
    /// Scenario: User requests setup guide with chat ID
    ///   Given a user is authenticated
    ///   And a game exists
    ///   And a chat session exists
    ///   When user requests a setup guide with chat ID
    ///   Then the setup guide is returned
    ///   And the request is logged to the chat
    /// </summary>
    [Fact]
    public async Task GivenChatId_WhenRequestingSetupGuide_ThenLogsToChat()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-chat-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Chat Enabled Game");

        // And: A chat session exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"setup-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Setup Agent",
            Kind = "setup",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chatId = Guid.NewGuid();
        var chat = new ChatEntity
        {
            Id = chatId,
            GameId = game.Id,
            AgentId = agent.Id,
            UserId = user.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // When: User requests a setup guide with chat ID
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, chatId))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: The setup guide is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: The request is logged to the chat
        var chatLogs = await db.ChatLogs
            .Where(log => log.ChatId == chatId)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        Assert.NotEmpty(chatLogs);
        Assert.Contains(chatLogs, log => log.Level == "user" && log.Message.Contains("Generate setup guide"));
        Assert.Contains(chatLogs, log => log.Level == "assistant" && log.Message.Contains("Setup guide"));
    }

    /// <summary>
    /// Scenario: User requests setup guide and receives estimated time
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests a setup guide
    ///   Then the response includes estimated setup time
    ///   And the estimated time is reasonable (between 5 and 30 minutes)
    /// </summary>
    [Fact]
    public async Task GivenGameSetup_WhenRequestingGuide_ThenIncludesEstimatedTime()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-time-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Timed Setup Game");

        // When: User requests a setup guide
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: The response includes estimated setup time
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);

        Assert.NotNull(setupGuide);
        Assert.True(setupGuide!.estimatedSetupTimeMinutes > 0);

        // And: The estimated time is reasonable (between 5 and 30 minutes)
        Assert.InRange(setupGuide.estimatedSetupTimeMinutes, 5, 30);
    }

    /// <summary>
    /// Scenario: Setup guide response includes token usage
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests a setup guide
    ///   Then the response includes token usage information
    ///   And total tokens equals prompt tokens plus completion tokens
    /// </summary>
    [Fact]
    public async Task GivenSetupGuideGeneration_WhenComplete_ThenIncludesTokenUsage()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-tokens-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Token Tracked Game");

        // When: User requests a setup guide
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: The response includes token usage information
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);

        Assert.NotNull(setupGuide);

        // And: Total tokens equals prompt tokens plus completion tokens (if LLM was used)
        if (setupGuide!.totalTokens > 0)
        {
            Assert.Equal(setupGuide.totalTokens, setupGuide.promptTokens + setupGuide.completionTokens);
        }
    }

    /// <summary>
    /// Scenario: Setup guide request is logged for monitoring
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests a setup guide
    ///   Then the request is logged in ai_request_logs
    ///   And the log includes user ID, game ID, endpoint, and latency
    /// </summary>
    [Fact]
    public async Task GivenSetupGuideRequest_WhenComplete_ThenLogsRequest()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-log-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Logged Game");

        // When: User requests a setup guide
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Then: The request is logged in ai_request_logs
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logs = await db.AiRequestLogs
            .Where(log => log.UserId == user.Id && log.GameId == game.Id && log.Endpoint == "setup")
            .ToListAsync();

        // And: The log includes user ID, game ID, endpoint, and latency
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal(user.Id, log.UserId);
        Assert.Equal(game.Id, log.GameId);
        Assert.Equal("setup", log.Endpoint);
        Assert.True(log.LatencyMs > 0);
    }

    /// <summary>
    /// Scenario: Multiple users request setup guides concurrently
    ///   Given multiple users are authenticated
    ///   And a game exists
    ///   When all users request setup guides simultaneously
    ///   Then all requests succeed
    ///   And each user receives a valid setup guide
    /// </summary>
    [Fact]
    public async Task GivenMultipleUsers_WhenRequestingSetupGuideConcurrently_ThenAllSucceed()
    {
        // Given: Multiple users are authenticated
        var user1 = await CreateTestUserAsync($"setup-concurrent-1-{TestRunId}", UserRole.User);
        var user2 = await CreateTestUserAsync($"setup-concurrent-2-{TestRunId}", UserRole.User);
        var user3 = await CreateTestUserAsync($"setup-concurrent-3-{TestRunId}", UserRole.User);

        var cookies1 = await AuthenticateUserAsync(user1.Email);
        var cookies2 = await AuthenticateUserAsync(user2.Email);
        var cookies3 = await AuthenticateUserAsync(user3.Email);

        // And: A game exists
        var game = await CreateTestGameAsync("Concurrent Test Game");

        // When: All users request setup guides simultaneously
        var client1 = CreateClientWithoutCookies();
        var client2 = CreateClientWithoutCookies();
        var client3 = CreateClientWithoutCookies();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request1, cookies1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request2, cookies2);

        var request3 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request3, cookies3);

        var tasks = new[]
        {
            client1.SendAsync(request1),
            client2.SendAsync(request2),
            client3.SendAsync(request3)
        };

        var responses = await Task.WhenAll(tasks);

        // Then: All requests succeed
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        // And: Each user receives a valid setup guide
        foreach (var response in responses)
        {
            var json = await response.Content.ReadAsStringAsync();
            var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);
            Assert.NotNull(setupGuide);
            Assert.NotEmpty(setupGuide!.steps);
        }
    }

    /// <summary>
    /// Scenario: Setup guide includes confidence score when available
    ///   Given a user is authenticated
    ///   And a game with RAG data exists
    ///   When user requests a setup guide
    ///   Then the response includes a confidence score
    ///   And the confidence score is between 0 and 1
    /// </summary>
    [Fact]
    public async Task GivenRagData_WhenRequestingSetupGuide_ThenIncludesConfidenceScore()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"setup-confidence-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with RAG data exists
        var game = await CreateTestGameAsync("Confidence Score Game");

        // When: User requests a setup guide
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/setup")
        {
            Content = JsonContent.Create(new SetupGuideRequest(game.Id, null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var setupGuide = JsonSerializer.Deserialize<SetupGuideResponse>(json, JsonOptions);

        Assert.NotNull(setupGuide);

        // Then: The response includes a confidence score (if RAG data was found)
        // And: The confidence score is between 0 and 1 (if present)
        if (setupGuide!.confidence.HasValue)
        {
            Assert.InRange(setupGuide.confidence.Value, 0.0, 1.0);
        }
    }
}
