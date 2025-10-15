using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for chat management endpoints.
///
/// Feature: Chat UI for Meeple Agents (UI-01)
/// As a user
/// I want to create and manage chat sessions with AI agents
/// So that I can maintain conversation history across sessions
/// </summary>
public class ChatEndpointsTests : IntegrationTestBase
{
    public ChatEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: User creates a new chat session
    ///   Given authenticated user and valid game/agent exist
    ///   When user posts to /chats with gameId and agentId
    ///   Then chat is created with HTTP 201
    ///   And response contains chat ID and metadata
    /// </summary>
    [Fact]
    public async Task PostChats_CreatesChat_ForAuthenticatedUser()
    {
        // Given: Authenticated user and valid game/agent
        var user = await CreateTestUserAsync("chat-creator");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");

        // When: User creates a chat
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chats")
        {
            Content = JsonContent.Create(new CreateChatRequest(game.Id, agent.Id))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Chat is created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var chat = await response.Content.ReadFromJsonAsync<ChatDto>();
        Assert.NotNull(chat);
        Assert.NotEqual(Guid.Empty, chat!.Id);
        Assert.Equal(game.Id, chat.GameId);
        Assert.Equal(agent.Id, chat.AgentId);
        Assert.NotEqual(default, chat.StartedAt);

        // Verify persistence
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var stored = await db.Chats.FindAsync(chat.Id);
        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored!.UserId);
    }

    /// <summary>
    /// Scenario: Unauthenticated user cannot create chat
    ///   Given user is not authenticated
    ///   When user tries to create chat
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task PostChats_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = CreateClientWithoutCookies();
        var response = await client.PostAsJsonAsync("/api/v1/chats",
            new CreateChatRequest("catan", "catan-qa"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User retrieves their chat sessions
    ///   Given user has multiple chat sessions
    ///   When user gets /chats
    ///   Then all user's chats are returned ordered by recency
    ///   And other users' chats are not included
    /// </summary>
    [Fact]
    public async Task GetChats_ReturnsUserChats_OrderedByRecency()
    {
        // Given: User with multiple chats
        var user = await CreateTestUserAsync("multi-chat-user");
        var otherUser = await CreateTestUserAsync("other-user");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");

        // Create multiple chats for user
        var chat1 = await CreateTestChatAsync(user.Id, game.Id, agent.Id);
        await Task.Delay(100); // Ensure time difference
        var chat2 = await CreateTestChatAsync(user.Id, game.Id, agent.Id);

        // Create chat for other user (should not be returned)
        await CreateTestChatAsync(otherUser.Id, game.Id, agent.Id);

        // When: User retrieves their chats
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chats");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Only user's chats returned, ordered by recency
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chats = await response.Content.ReadFromJsonAsync<List<ChatDto>>();
        Assert.NotNull(chats);
        Assert.Equal(2, chats!.Count);
        Assert.Equal(chat2.Id, chats[0].Id); // Most recent first
        Assert.Equal(chat1.Id, chats[1].Id);
    }

    /// <summary>
    /// Scenario: User retrieves specific chat with history
    ///   Given user owns a chat with messages
    ///   When user gets /chats/{chatId}
    ///   Then chat details with full message history are returned
    /// </summary>
    [Fact]
    public async Task GetChatById_ReturnsFullChatWithHistory()
    {
        // Given: User with chat containing messages
        var user = await CreateTestUserAsync("history-viewer");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");
        var chat = await CreateTestChatAsync(user.Id, game.Id, agent.Id);

        // Add messages to chat
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.ChatLogs.Add(new ChatLogEntity
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "How do I setup?",
                CreatedAt = DateTime.UtcNow
            });
            db.ChatLogs.Add(new ChatLogEntity
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Here are the setup instructions...",
                CreatedAt = DateTime.UtcNow.AddSeconds(1)
            });
            await db.SaveChangesAsync();
        }

        // When: User retrieves chat by ID
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Full chat with history returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatWithHistory = await response.Content.ReadFromJsonAsync<ChatWithHistoryDto>();
        Assert.NotNull(chatWithHistory);
        Assert.Equal(chat.Id, chatWithHistory!.Id);
        Assert.Equal(2, chatWithHistory.Messages.Count);
        Assert.Equal("user", chatWithHistory.Messages[0].Level);
        Assert.Equal("assistant", chatWithHistory.Messages[1].Level);
    }

    /// <summary>
    /// Scenario: User cannot access another user's chat
    ///   Given chat belongs to different user
    ///   When user tries to get that chat
    ///   Then HTTP 404 Not Found is returned
    /// </summary>
    [Fact]
    public async Task GetChatById_ReturnsNotFound_WhenNotOwner()
    {
        var user = await CreateTestUserAsync("non-owner");
        var owner = await CreateTestUserAsync("owner");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");
        var chat = await CreateTestChatAsync(owner.Id, game.Id, agent.Id);

        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User deletes their own chat
    ///   Given user owns a chat
    ///   When user deletes the chat
    ///   Then HTTP 204 No Content is returned
    ///   And chat is removed from database
    /// </summary>
    [Fact]
    public async Task DeleteChat_DeletesChat_ForOwner()
    {
        var user = await CreateTestUserAsync("deleter");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");
        var chat = await CreateTestChatAsync(user.Id, game.Id, agent.Id);

        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/chats/{chat.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify chat was deleted
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var stored = await db.Chats.FindAsync(chat.Id);
        Assert.Null(stored);
    }

    /// <summary>
    /// Scenario: User cannot delete another user's chat
    ///   Given chat belongs to different user
    ///   When user tries to delete that chat
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DeleteChat_ReturnsForbidden_WhenNotOwner()
    {
        var user = await CreateTestUserAsync("non-owner-deleter");
        var owner = await CreateTestUserAsync("chat-owner");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");
        var chat = await CreateTestChatAsync(owner.Id, game.Id, agent.Id);

        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/chats/{chat.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify chat still exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var stored = await db.Chats.FindAsync(chat.Id);
        Assert.NotNull(stored);
    }

    /// <summary>
    /// Scenario: User retrieves agents for a game
    ///   Given game has multiple agents
    ///   When user gets /games/{gameId}/agents
    ///   Then all agents for that game are returned
    /// </summary>
    [Fact]
    public async Task GetGameAgents_ReturnsAllAgentsForGame()
    {
        var user = await CreateTestUserAsync("agent-viewer");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Catan");
        var qaAgent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");
        var explainAgent = await CreateTestAgentAsync(game.Id, "explain", "Explain Agent");

        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/agents");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var agents = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        Assert.NotNull(agents);
        Assert.Equal(2, agents!.Count);
        Assert.Contains(agents, a => a.Id == qaAgent.Id && a.Kind == "qa");
        Assert.Contains(agents, a => a.Id == explainAgent.Id && a.Kind == "explain");
    }

    /// <summary>
    /// Scenario: Filter chats by game
    ///   Given user has chats for multiple games
    ///   When user gets /chats?gameId={gameId}
    ///   Then only chats for that game are returned
    /// </summary>
    [Fact]
    public async Task GetChats_WithGameIdFilter_ReturnsOnlyGameChats()
    {
        var user = await CreateTestUserAsync("game-filter-user");
        var cookies = await AuthenticateUserAsync(user.Email);
        var game1 = await CreateTestGameAsync("Catan");
        var game2 = await CreateTestGameAsync("Ticket to Ride");
        var agent1 = await CreateTestAgentAsync(game1.Id, "qa", "Q&A Agent");
        var agent2 = await CreateTestAgentAsync(game2.Id, "qa", "Q&A Agent");

        var chat1 = await CreateTestChatAsync(user.Id, game1.Id, agent1.Id);
        var chat2 = await CreateTestChatAsync(user.Id, game2.Id, agent2.Id);

        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats?gameId={game1.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chats = await response.Content.ReadFromJsonAsync<List<ChatDto>>();
        Assert.NotNull(chats);
        Assert.Single(chats!);
        Assert.Equal(chat1.Id, chats[0].Id);
        Assert.Equal(game1.Id, chats[0].GameId);
    }

    // Helper methods
    private async Task<ChatEntity> CreateTestChatAsync(string userId, string gameId, string agentId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AgentId = agentId,
            StartedAt = DateTime.UtcNow,
            LastMessageAt = null
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // Note: Chat cleanup handled by cascade delete when game is deleted
        return chat;
    }

    private async Task<AgentEntity> CreateTestAgentAsync(string gameId, string kind, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"{gameId}-{kind}",
            GameId = gameId,
            Name = name,
            Kind = kind,
            CreatedAt = DateTime.UtcNow
        };

        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        // Note: Agent cleanup handled by cascade delete when game is deleted
        return agent;
    }
}
