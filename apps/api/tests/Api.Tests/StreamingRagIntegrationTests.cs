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
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// API-02: Integration tests for streaming explain RAG endpoint (SSE)
/// Tests cover authentication, SSE format, event ordering, and error handling
/// </summary>
public class StreamingRagIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StreamingRagIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
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
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = await ParseSseEventsAsync(response);

        // And: Single error event is emitted
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
        var errorData = JsonSerializer.Deserialize<StreamingError>(
            ((JsonElement)events[0].Data!).GetRawText(), JsonOptions);
        Assert.Equal("Please provide a topic to explain.", errorData!.errorMessage);
        Assert.Equal("EMPTY_TOPIC", errorData.errorCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Cache-Control"));
        Assert.Contains("no-cache", response.Headers.GetValues("Cache-Control"));
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
        Assert.NotEmpty(events);

        var eventTypes = events.Select(e => e.Type).ToList();

        // Verify expected sequence: StateUpdate(s) -> Citations -> Outline -> ScriptChunk(s) -> Complete
        int idx = 0;

        // StateUpdate: Generating embeddings
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate);
        idx++;

        // StateUpdate: Searching vector database
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate);
        idx++;

        // Citations
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.Citations);
        idx++;

        // StateUpdate: Building outline
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate);
        idx++;

        // Outline
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.Outline);
        idx++;

        // StateUpdate: Generating script
        Assert.True(idx < eventTypes.Count && eventTypes[idx] == StreamingEventType.StateUpdate);
        idx++;

        // One or more ScriptChunks
        int scriptChunkCount = 0;
        while (idx < eventTypes.Count - 1 && eventTypes[idx] == StreamingEventType.ScriptChunk)
        {
            scriptChunkCount++;
            idx++;
        }
        Assert.True(scriptChunkCount > 0, "Expected at least one ScriptChunk event");

        // Complete
        Assert.Equal(StreamingEventType.Complete, eventTypes[^1]);
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
        Assert.NotNull(citationsEvent);

        var citationsData = JsonSerializer.Deserialize<StreamingCitations>(
            ((JsonElement)citationsEvent.Data!).GetRawText(), JsonOptions);

        Assert.NotNull(citationsData);
        Assert.NotEmpty(citationsData!.citations);
        Assert.All(citationsData.citations, citation =>
        {
            Assert.NotEmpty(citation.text);
            Assert.StartsWith("PDF:", citation.source);
            Assert.True(citation.page > 0);
        });
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
        Assert.NotNull(outlineEvent);

        var outlineData = JsonSerializer.Deserialize<StreamingOutline>(
            ((JsonElement)outlineEvent.Data!).GetRawText(), JsonOptions);

        Assert.NotNull(outlineData);
        Assert.Equal("game setup", outlineData!.outline.mainTopic);
        Assert.NotEmpty(outlineData.outline.sections);
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
        Assert.NotEmpty(scriptChunkEvents);

        for (int i = 0; i < scriptChunkEvents.Count; i++)
        {
            var chunkData = JsonSerializer.Deserialize<StreamingScriptChunk>(
                ((JsonElement)scriptChunkEvents[i].Data!).GetRawText(), JsonOptions);

            Assert.NotNull(chunkData);
            Assert.Equal(i, chunkData!.chunkIndex);
            Assert.Equal(scriptChunkEvents.Count, chunkData.totalChunks);
            Assert.NotEmpty(chunkData.chunk);
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
        Assert.NotNull(completeEvent);

        var completeData = JsonSerializer.Deserialize<StreamingComplete>(
            ((JsonElement)completeEvent.Data!).GetRawText(), JsonOptions);

        Assert.NotNull(completeData);
        Assert.True(completeData!.estimatedReadingTimeMinutes > 0);
        Assert.True(completeData.confidence >= 0 && completeData.confidence <= 1);
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
        Assert.All(events, evt =>
        {
            Assert.NotEqual(default(DateTime), evt.Timestamp);
            Assert.True(evt.Timestamp <= DateTime.UtcNow.AddSeconds(5)); // Allow 5 sec for test execution
        });
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
        Assert.NotNull(errorEvent);

        var errorData = JsonSerializer.Deserialize<StreamingError>(
            ((JsonElement)errorEvent.Data!).GetRawText(), JsonOptions);
        Assert.NotNull(errorData);
        Assert.Contains("NO_RESULTS", errorData!.errorCode ?? "");
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
        Assert.StartsWith("data: ", rawContent);
        Assert.EndsWith("\n\n", rawContent);

        // And: Can parse as valid JSON
        var jsonStart = rawContent.IndexOf('{');
        var jsonEnd = rawContent.LastIndexOf('}') + 1;
        var json = rawContent.Substring(jsonStart, jsonEnd - jsonStart);

        var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.TryGetProperty("Type", out _));
        Assert.True(parsed.RootElement.TryGetProperty("Data", out _));
        Assert.True(parsed.RootElement.TryGetProperty("Timestamp", out _));
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
