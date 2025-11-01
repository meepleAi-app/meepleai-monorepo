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
using Api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// API-02: Integration tests for streaming explain RAG endpoint (SSE)
/// Tests cover authentication, SSE format, event ordering, and error handling
/// </summary>
[Collection("Postgres Integration Tests")]
public class StreamingRagIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StreamingRagIntegrationTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    [Fact]
    public async Task ExplainStream_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: Unauthenticated client
        var client = CreateClientWithoutCookies();

        // When: Posting to streaming explain endpoint
        var response = await client.PostAsJsonAsync("/api/v1/agents/explain/stream",
            new ExplainRequest("test-game", "setup rules"));

        // Then: Returns 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExplainStream_WithEmptyTopic_ReturnsErrorEvent()
    {
        // Given: Authenticated user
        var user = await CreateTestUserAsync("stream-user");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Posting with empty topic
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest("test-game", ""))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Returns 200 OK with SSE stream
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        var events = await ParseSseEventsAsync(response);

        // And: Single error event is emitted
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
        var errorData = JsonSerializer.Deserialize<StreamingError>(
            ((JsonElement)events[0].Data!).GetRawText(), JsonOptions);
        errorData!.errorMessage.Should().Be("Please provide a topic to explain.");
        errorData.errorCode.Should().Be("EMPTY_TOPIC");
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_ReturnsSseHeaders()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Posting to streaming endpoint
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Returns correct SSE headers
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        response.Headers.Contains("Cache-Control").Should().BeTrue();
        response.Headers.GetValues("Cache-Control").Should().Contain("no-cache");
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_EmitsEventsInCorrectOrder()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Events are emitted in correct order
        events.Should().NotBeEmpty();

        var eventTypes = events.Select(e => e.Type).ToList();

        // Verify expected sequence: StateUpdate(s) -> Citations -> Outline -> ScriptChunk(s) -> Complete
        int idx = 0;

        // StateUpdate: Generating embeddings
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate).Should().BeTrue();
        idx++;

        // StateUpdate: Searching vector database
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate).Should().BeTrue();
        idx++;

        // Citations
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.Citations).Should().BeTrue();
        idx++;

        // StateUpdate: Building outline
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate).Should().BeTrue();
        idx++;

        // Outline
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.Outline).Should().BeTrue();
        idx++;

        // StateUpdate: Generating script
        (idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate).Should().BeTrue();
        idx++;

        // One or more ScriptChunks
        int scriptChunkCount = 0;
        while (idx < eventTypes.Count - 1 && eventTypes[idx] == StreamingEventType.ScriptChunk)
        {
            scriptChunkCount++;
            idx++;
        }
        (scriptChunkCount > 0).Should().BeTrue("Expected at least one ScriptChunk event");

        // Complete
        eventTypes[^1].Should().Be(StreamingEventType.Complete);
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_CitationsContainCorrectData()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Citations event contains valid snippets
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        citationsEvent.Should().NotBeNull();

        var citationsData = JsonSerializer.Deserialize<StreamingCitations>(
            ((JsonElement)citationsEvent.Data!).GetRawText(), JsonOptions);

        citationsData.Should().NotBeNull();
        citationsData!.citations.Should().NotBeEmpty();
        citationsData.citations.Should().OnlyContain(citation =>
            !string.IsNullOrEmpty(citation.text) &&
            citation.source.StartsWith("PDF:") &&
            citation.page > 0);
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_OutlineContainsMainTopic()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Outline event contains main topic
        var outlineEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Outline);
        outlineEvent.Should().NotBeNull();

        var outlineData = JsonSerializer.Deserialize<StreamingOutline>(
            ((JsonElement)outlineEvent.Data!).GetRawText(), JsonOptions);

        outlineData.Should().NotBeNull();
        outlineData!.outline.mainTopic.Should().Be("game setup");
        outlineData.outline.sections.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_ScriptChunksHaveCorrectMetadata()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Script chunks have correct index and total
        var scriptChunkEvents = events.Where(e => e.Type == StreamingEventType.ScriptChunk).ToList();
        scriptChunkEvents.Should().NotBeEmpty();

        for (int i = 0; i < scriptChunkEvents.Count; i++)
        {
            var chunkData = JsonSerializer.Deserialize<StreamingScriptChunk>(
                ((JsonElement)scriptChunkEvents[i].Data!).GetRawText(), JsonOptions);

            chunkData.Should().NotBeNull();
            chunkData!.chunkIndex.Should().Be(i);
            chunkData.totalChunks.Should().Be(scriptChunkEvents.Count);
            chunkData.chunk.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ExplainStream_WithValidRequest_CompleteEventContainsMetadata()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Complete event contains metadata
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();

        var completeData = JsonSerializer.Deserialize<StreamingComplete>(
            ((JsonElement)completeEvent.Data!).GetRawText(), JsonOptions);

        completeData.Should().NotBeNull();
        (completeData!.estimatedReadingTimeMinutes > 0).Should().BeTrue();
        (completeData.confidence >= 0 && completeData.confidence <= 1).Should().BeTrue();
    }

    [Fact]
    public async Task ExplainStream_AllEvents_HaveTimestamps()
    {
        // Given: Authenticated user and game with vector data
        var user = await CreateTestUserAsync("stream-user");
        var game = await CreateTestGameAsync("Test Game");
        await SeedVectorDataAsync(game.Id, user.Id);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest(game.Id, "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: All events have valid timestamps
        events.Should().OnlyContain(evt =>
            evt.Timestamp != default(DateTime) &&
            evt.Timestamp <= DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task ExplainStream_WithNonExistentGame_ReturnsErrorEvent()
    {
        // Given: Authenticated user but non-existent game
        var user = await CreateTestUserAsync("stream-user");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation for non-existent game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest("non-existent-game", "game setup"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var events = await ParseSseEventsAsync(response);

        // Then: Error event is emitted (no vector data found)
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().NotBeNull();

        var errorData = JsonSerializer.Deserialize<StreamingError>(
            ((JsonElement)errorEvent.Data!).GetRawText(), JsonOptions);
        errorData.Should().NotBeNull();
        (errorData!.errorCode ?? "").Should().Contain("NO_RESULTS");
    }

    [Fact]
    public async Task ExplainStream_SseFormat_IsCorrect()
    {
        // Given: Authenticated user and game
        var user = await CreateTestUserAsync("stream-user");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Requesting explanation with empty topic (quick error response)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/explain/stream")
        {
            Content = JsonContent.Create(new ExplainRequest("test-game", ""))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);
        var rawContent = await response.Content.ReadAsStringAsync();

        // Then: SSE format is correct (data: {json}\n\n)
        rawContent.Should().StartWith("data: ");
        rawContent.Should().EndWith("\n\n");

        // And: Can parse as valid JSON
        var jsonStart = rawContent.IndexOf('{');
        var jsonEnd = rawContent.LastIndexOf('}') + 1;
        var json = rawContent.Substring(jsonStart, jsonEnd - jsonStart);

        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.TryGetProperty("Type", out _).Should().BeTrue();
        parsed.RootElement.TryGetProperty("Data", out _).Should().BeTrue();
        parsed.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
    }

    // Helper methods

    private static async Task<List<RagStreamingEvent>> ParseSseEventsAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var events = new List<RagStreamingEvent>();

        // Parse SSE format: "data: {json}\n\n"
        var lines = content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6); // Remove "data: " prefix
                var evt = JsonSerializer.Deserialize<RagStreamingEvent>(json, JsonOptions);
                if (evt != null)
                {
                    events.Add(evt);
                }
            }
        }

        return events;
    }

    private async Task SeedVectorDataAsync(string gameId, string userId)
    {
        // Seed minimal vector data for testing
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

        // Create a PDF document
        var pdfId = $"pdf-{TestRunId}-{Guid.NewGuid():N}";
        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test-rules.pdf",
            FilePath = "/test/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow
        };

        db.PdfDocuments.Add(pdf);

        // Create a VectorDocumentEntity to track that this PDF has been indexed
        var vectorDoc = new VectorDocumentEntity
        {
            Id = Guid.NewGuid().ToString(),
            PdfDocumentId = pdfId,
            GameId = gameId,
            ChunkCount = 2,
            TotalCharacters = 130,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };

        db.VectorDocuments.Add(vectorDoc);
        await db.SaveChangesAsync();

        // Index actual chunks in Qdrant with embeddings
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Game setup instructions: Place the board in the center of the table.",
                Embedding = CreateRandomEmbedding(),
                Page = 1,
                CharStart = 0,
                CharEnd = 70
            },
            new()
            {
                Text = "Each player receives 5 cards from the shuffled deck.",
                Embedding = CreateRandomEmbedding(),
                Page = 2,
                CharStart = 0,
                CharEnd = 54
            }
        };

        await qdrantService.IndexDocumentChunksAsync(gameId, pdfId, chunks);

        TrackPdfDocumentId(pdfId);
    }

    /// <summary>
    /// Creates a random embedding vector of size 1536 (matching text-embedding-3-small)
    /// </summary>
    private static float[] CreateRandomEmbedding()
    {
        var random = new Random();
        var embedding = new float[1536];

        // Generate normalized random vector
        float sum = 0;
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
            sum += embedding[i] * embedding[i];
        }

        // Normalize
        float magnitude = (float)Math.Sqrt(sum);
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
