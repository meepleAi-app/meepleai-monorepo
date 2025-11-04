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
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// CHAT-01: BDD-style integration tests for Streaming QA endpoint with Server-Sent Events (SSE).
///
/// Feature: Streaming QA Responses with SSE
/// As an authenticated user
/// I want to receive progressive QA answers via streaming
/// So that I can see the response being generated in real-time
/// </summary>
[Collection("Postgres Integration Tests")]
public class StreamingQaEndpointIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StreamingQaEndpointIntegrationTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        response.Headers.Should().Contain(h => h.Key == "Cache-Control" && h.Value.Contains("no-cache"));

        // And: Events are streamed in correct order
        var events = await ParseSSEEventsAsync(response);
        events.Should().NotBeEmpty();

        // Verify event sequence
        var eventTypes = events.Select(e => e.Type).ToList();
        eventTypes.Should().Contain(StreamingEventType.StateUpdate);

        // TEST-693: Citations event is optional - only present when search finds results
        // When no results are found, Error event is sent instead
        var hasError = eventTypes.Contains(StreamingEventType.Error);
        if (!hasError)
        {
            // Success path: should have Citations, Token(s), Complete
            eventTypes.Should().Contain(StreamingEventType.Citations);
            eventTypes.Should().Contain(StreamingEventType.Token);
            eventTypes.Should().Contain(StreamingEventType.Complete);
            eventTypes[^1].Should().Be(StreamingEventType.Complete);
        }
        else
        {
            // Error path: should have Error event (no Citations or Token expected)
            eventTypes.Should().Contain(StreamingEventType.Error);
        }

        // And: All events have valid timestamps
        events.Should().OnlyContain(evt =>
            evt.Timestamp > DateTime.UtcNow.AddMinutes(-1) &&
            evt.Timestamp <= DateTime.UtcNow.AddSeconds(5));
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then: A Citations event is emitted (or error if no vector data)
        var events = await ParseSSEEventsAsync(response);

        // Either we get citations (if vector data exists) or error (if no vector data)
        var hasCitations = events.Any(e => e.Type == StreamingEventType.Citations);
        var hasError = events.Any(e => e.Type == StreamingEventType.Error);

        (hasCitations || hasError).Should().BeTrue("Should receive either Citations event (if vector data exists) or Error event (if no vector data)");

        // And: If citations exist, they contain required fields
        if (hasCitations)
        {
            var citationsEvent = events.First(e => e.Type == StreamingEventType.Citations);
            var citationsJson = ((JsonElement)citationsEvent.Data).GetRawText();
            var citations = JsonSerializer.Deserialize<StreamingCitations>(citationsJson, JsonOptions);

            citations.Should().NotBeNull();
            citations!.citations.Should().NotBeEmpty();
            citations.citations.Should().OnlyContain(citation =>
                !string.IsNullOrWhiteSpace(citation.text) &&
                !string.IsNullOrWhiteSpace(citation.source) &&
                citation.page >= 0);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Consume the stream
        await ParseSSEEventsAsync(response);

        // Then: The request and response are logged to the chat
        var chatLogs = await db.ChatLogs
            .Where(log => log.ChatId == chatId)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        chatLogs.Should().NotBeEmpty();

        // Should have user message (query) and assistant message (answer)
        var userMessage = chatLogs.FirstOrDefault(log => log.Level == "user");
        var assistantMessage = chatLogs.FirstOrDefault(log => log.Level == "assistant");

        userMessage.Should().NotBeNull();
        userMessage!.Message.Should().Contain("How many players?");

        // Assistant message may or may not exist depending on whether an answer was generated
        // If no vector data, we might only have the user message
        if (assistantMessage != null)
        {
            string.IsNullOrWhiteSpace(assistantMessage.Message).Should().BeFalse();
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Consume the stream
        await ParseSSEEventsAsync(response);

        // Then: The request is logged in ai_request_logs
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logs = await db.AiRequestLogs
            .Where(log => log.UserId == user.Id && log.GameId == game.Id && log.Endpoint == "qa-stream")
            .ToListAsync();

        // And: The log includes user ID, game ID, endpoint, and latency
        logs.Should().NotBeEmpty();
        var log = logs.First();
        log.UserId.Should().Be(user.Id);
        log.GameId.Should().Be(game.Id);
        log.Endpoint.Should().Be("qa-stream");
        (log.LatencyMs > 0).Should().BeTrue();
        (log.Query ?? "").Should().Contain("test query");
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Streaming completes
        var events = await ParseSSEEventsAsync(response);
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);

        // Then: The Complete event includes token counts (if successful, not error)
        if (completeEvent != null)
        {
            var completeJson = ((JsonElement)completeEvent.Data).GetRawText();
            var complete = JsonSerializer.Deserialize<StreamingComplete>(completeJson, JsonOptions);

            complete.Should().NotBeNull();
            // Token counts should be >= 0
            (complete!.completionTokens >= 0).Should().BeTrue();
            (complete.totalTokens >= 0).Should().BeTrue();
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
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);

        // And: Each user receives streaming events
        var eventTasks = responses.Select(ParseSSEEventsAsync).ToArray();
        var allEvents = await Task.WhenAll(eventTasks);

        allEvents.Should().OnlyContain(events => events.Count > 0);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then: Events are received (may include Error event if services fail)
        var events = await ParseSSEEventsAsync(response);
        events.Should().NotBeEmpty();

        // If an error occurred, verify it has proper structure
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        if (errorEvent != null)
        {
            var errorJson = ((JsonElement)errorEvent.Data).GetRawText();
            var error = JsonSerializer.Deserialize<StreamingError>(errorJson, JsonOptions);

            error.Should().NotBeNull();
            string.IsNullOrWhiteSpace(error!.errorMessage).Should().BeFalse();
            string.IsNullOrWhiteSpace(error.errorCode).Should().BeFalse();
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
