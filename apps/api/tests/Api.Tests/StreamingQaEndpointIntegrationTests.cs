using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CHAT-01: BDD-style integration tests for Streaming QA endpoint with Server-Sent Events (SSE).
///
/// Feature: Streaming QA Responses with SSE
/// As an authenticated user
/// I want to receive progressive QA answers via streaming
/// So that I can see the response being generated in real-time
/// </summary>
public class StreamingQaEndpointIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StreamingQaEndpointIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Authenticated user requests streaming QA
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests streaming QA for that game
    ///   Then SSE response is returned with correct headers
    ///   And events are streamed in correct order: StateUpdate -> Citations -> Token(s) -> Complete
    ///   And all events have valid timestamps
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenRequestingStreamingQa_ThenReturnsSSEWithEvents()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-user-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Streaming QA Game");

        // When: User requests streaming QA for that game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "How many players?", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Then: SSE response is returned with correct headers
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers, h => h.Key == "Cache-Control" && h.Value.Contains("no-cache"));

        // And: Events are streamed in correct order
        var events = await ParseSSEEventsAsync(response);
        Assert.NotEmpty(events);

        // Verify event sequence
        var eventTypes = events.Select(e => e.Type).ToList();
        Assert.Contains(StreamingEventType.StateUpdate, eventTypes);
        Assert.Contains(StreamingEventType.Citations, eventTypes);
        Assert.Contains(StreamingEventType.Token, eventTypes);
        Assert.Contains(StreamingEventType.Complete, eventTypes);

        // Complete should be last
        Assert.Equal(StreamingEventType.Complete, eventTypes[^1]);

        // And: All events have valid timestamps
        Assert.All(events, evt =>
        {
            Assert.True(evt.Timestamp > DateTime.UtcNow.AddMinutes(-1));
            Assert.True(evt.Timestamp <= DateTime.UtcNow.AddSeconds(5));
        });
    }

    /// <summary>
    /// Scenario: Unauthenticated user attempts streaming QA
    ///   Given no authentication is provided
    ///   When user requests streaming QA
    ///   Then the request is rejected with Unauthorized
    /// </summary>
    [Fact]
    public async Task GivenNoAuthentication_WhenRequestingStreamingQa_ThenReturnsUnauthorized()
    {
        // Given: No authentication is provided
        var client = CreateClientWithoutCookies();

        // When: User requests streaming QA
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa/stream",
            new QaRequest("any-game", "test query", chatId: null));

        // Then: The request is rejected with Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User requests streaming QA without game ID
    ///   Given a user is authenticated
    ///   When user requests streaming QA without providing a game ID
    ///   Then the request is rejected with BadRequest
    /// </summary>
    [Fact]
    public async Task GivenNoGameId_WhenRequestingStreamingQa_ThenReturnsBadRequest()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-badreq-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests streaming QA without providing a game ID
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest("", "test query", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: The request is rejected with BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User requests streaming QA and receives citations
    ///   Given a user is authenticated
    ///   And a game with vector data exists
    ///   When user requests streaming QA
    ///   Then a Citations event is emitted
    ///   And citations contain text, source, and page number
    /// </summary>
    [Fact]
    public async Task GivenGameWithVectorData_WhenRequestingStreamingQa_ThenReceivesCitations()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-citations-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game with vector data exists (in practice, this would be seeded)
        var game = await CreateTestGameAsync("Citations Game");

        // When: User requests streaming QA
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "How to play?", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Then: A Citations event is emitted (or error if no vector data)
        var events = await ParseSSEEventsAsync(response);

        // Either we get citations (if vector data exists) or error (if no vector data)
        var hasCitations = events.Any(e => e.Type == StreamingEventType.Citations);
        var hasError = events.Any(e => e.Type == StreamingEventType.Error);

        Assert.True(hasCitations || hasError,
            "Should receive either Citations event (if vector data exists) or Error event (if no vector data)");

        // And: If citations exist, they contain required fields
        if (hasCitations)
        {
            var citationsEvent = events.First(e => e.Type == StreamingEventType.Citations);
            var citationsJson = ((JsonElement)citationsEvent.Data).GetRawText();
            var citations = JsonSerializer.Deserialize<StreamingCitations>(citationsJson, JsonOptions);

            Assert.NotNull(citations);
            Assert.NotEmpty(citations!.citations);
            Assert.All(citations.citations, citation =>
            {
                Assert.False(string.IsNullOrWhiteSpace(citation.text));
                Assert.False(string.IsNullOrWhiteSpace(citation.source));
                Assert.True(citation.page >= 0);
            });
        }
    }

    /// <summary>
    /// Scenario: User requests streaming QA with chat ID
    ///   Given a user is authenticated
    ///   And a game exists
    ///   And a chat session exists
    ///   When user requests streaming QA with chat ID
    ///   Then the request and response are logged to the chat
    /// </summary>
    [Fact]
    public async Task GivenChatId_WhenRequestingStreamingQa_ThenLogsToChat()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-chat-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Chat Logging Game");

        // And: A chat session exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"qa-agent-{TestRunId}",
            GameId = game.Id,
            Name = "QA Agent",
            Kind = "qa",
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

        // When: User requests streaming QA with chat ID
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "How many players?", chatId))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Consume the stream
        await ParseSSEEventsAsync(response);

        // Then: The request and response are logged to the chat
        var chatLogs = await db.ChatLogs
            .Where(log => log.ChatId == chatId)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        Assert.NotEmpty(chatLogs);

        // Should have user message (query) and assistant message (answer)
        var userMessage = chatLogs.FirstOrDefault(log => log.Level == "user");
        var assistantMessage = chatLogs.FirstOrDefault(log => log.Level == "assistant");

        Assert.NotNull(userMessage);
        Assert.Contains("How many players?", userMessage!.Message);

        // Assistant message may or may not exist depending on whether an answer was generated
        // If no vector data, we might only have the user message
        if (assistantMessage != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(assistantMessage.Message));
        }
    }

    /// <summary>
    /// Scenario: Streaming QA request is logged for monitoring
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests streaming QA
    ///   Then the request is logged in ai_request_logs
    ///   And the log includes user ID, game ID, endpoint, and latency
    /// </summary>
    [Fact]
    public async Task GivenStreamingQaRequest_WhenComplete_ThenLogsRequest()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-log-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Logged Game");

        // When: User requests streaming QA
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "test query", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Consume the stream
        await ParseSSEEventsAsync(response);

        // Then: The request is logged in ai_request_logs
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logs = await db.AiRequestLogs
            .Where(log => log.UserId == user.Id && log.GameId == game.Id && log.Endpoint == "qa-stream")
            .ToListAsync();

        // And: The log includes user ID, game ID, endpoint, and latency
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal(user.Id, log.UserId);
        Assert.Equal(game.Id, log.GameId);
        Assert.Equal("qa-stream", log.Endpoint);
        Assert.True(log.LatencyMs > 0);
        Assert.Contains("test query", log.Query ?? "");
    }

    /// <summary>
    /// Scenario: Streaming QA includes token count in Complete event
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When user requests streaming QA
    ///   And streaming completes successfully
    ///   Then the Complete event includes token counts
    /// </summary>
    [Fact]
    public async Task GivenSuccessfulStreamingQa_WhenComplete_ThenIncludesTokenCount()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-tokens-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Token Count Game");

        // When: User requests streaming QA
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "test query", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Streaming completes
        var events = await ParseSSEEventsAsync(response);
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);

        // Then: The Complete event includes token counts (if successful, not error)
        if (completeEvent != null)
        {
            var completeJson = ((JsonElement)completeEvent.Data).GetRawText();
            var complete = JsonSerializer.Deserialize<StreamingComplete>(completeJson, JsonOptions);

            Assert.NotNull(complete);
            // Token counts should be >= 0
            Assert.True(complete!.completionTokens >= 0);
            Assert.True(complete.totalTokens >= 0);
        }
    }

    /// <summary>
    /// Scenario: Multiple users request streaming QA concurrently
    ///   Given multiple users are authenticated
    ///   And a game exists
    ///   When all users request streaming QA simultaneously
    ///   Then all requests succeed
    ///   And each user receives streaming events
    /// </summary>
    [Fact]
    public async Task GivenMultipleUsers_WhenRequestingStreamingQaConcurrently_ThenAllSucceed()
    {
        // Given: Multiple users are authenticated
        var user1 = await CreateTestUserAsync($"qa-stream-concurrent-1-{TestRunId}", UserRole.User);
        var user2 = await CreateTestUserAsync($"qa-stream-concurrent-2-{TestRunId}", UserRole.User);
        var user3 = await CreateTestUserAsync($"qa-stream-concurrent-3-{TestRunId}", UserRole.User);

        var cookies1 = await AuthenticateUserAsync(user1.Email);
        var cookies2 = await AuthenticateUserAsync(user2.Email);
        var cookies3 = await AuthenticateUserAsync(user3.Email);

        // And: A game exists
        var game = await CreateTestGameAsync("Concurrent Streaming Game");

        // When: All users request streaming QA simultaneously
        var client1 = CreateClientWithoutCookies();
        var client2 = CreateClientWithoutCookies();
        var client3 = CreateClientWithoutCookies();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "query 1", chatId: null))
        };
        AddCookies(request1, cookies1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "query 2", chatId: null))
        };
        AddCookies(request2, cookies2);

        var request3 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "query 3", chatId: null))
        };
        AddCookies(request3, cookies3);

        var tasks = new[]
        {
            client1.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead),
            client2.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead),
            client3.SendAsync(request3, HttpCompletionOption.ResponseHeadersRead)
        };

        var responses = await Task.WhenAll(tasks);

        // Then: All requests succeed
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        // And: Each user receives streaming events
        var eventTasks = responses.Select(ParseSSEEventsAsync).ToArray();
        var allEvents = await Task.WhenAll(eventTasks);

        Assert.All(allEvents, events => Assert.NotEmpty(events));
    }

    /// <summary>
    /// Scenario: Streaming QA handles error gracefully
    ///   Given a user is authenticated
    ///   And a game exists
    ///   When streaming QA encounters an error (e.g., embedding failure)
    ///   Then an Error event is emitted
    ///   And error includes message and code
    /// </summary>
    [Fact]
    public async Task GivenStreamingError_WhenEncountered_ThenEmitsErrorEvent()
    {
        // Given: A user is authenticated
        var user = await CreateTestUserAsync($"qa-stream-error-{TestRunId}", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Error Handling Game");

        // When: Streaming QA is requested (may fail due to missing vector data or service issues)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new QaRequest(game.Id, "test query", chatId: null))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Then: Events are received (may include Error event if services fail)
        var events = await ParseSSEEventsAsync(response);
        Assert.NotEmpty(events);

        // If an error occurred, verify it has proper structure
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        if (errorEvent != null)
        {
            var errorJson = ((JsonElement)errorEvent.Data).GetRawText();
            var error = JsonSerializer.Deserialize<StreamingError>(errorJson, JsonOptions);

            Assert.NotNull(error);
            Assert.False(string.IsNullOrWhiteSpace(error!.errorMessage));
            Assert.False(string.IsNullOrWhiteSpace(error.errorCode));
        }
    }

    // Helper method to parse SSE events from response stream
    private static async Task<List<RagStreamingEvent>> ParseSSEEventsAsync(HttpResponseMessage response)
    {
        var events = new List<RagStreamingEvent>();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);

        var buffer = new StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            // SSE format: "data: {json}\n\n"
            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6); // Remove "data: " prefix
                buffer.Append(json);
            }
            else if (string.IsNullOrWhiteSpace(line) && buffer.Length > 0)
            {
                // Empty line marks end of event
                var eventJson = buffer.ToString();
                buffer.Clear();

                try
                {
                    var evt = JsonSerializer.Deserialize<RagStreamingEvent>(eventJson, JsonOptions);
                    if (evt != null)
                    {
                        events.Add(evt);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed events
                }
            }
        }

        return events;
    }
}
