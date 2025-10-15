using Api.Infrastructure;
using Api.Models;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// API-02: Streaming RAG service for progressive explain responses via SSE
/// Emits events: StateUpdate -> Citations -> Outline -> ScriptChunk(s) -> Complete
/// </summary>
public class StreamingRagService : IStreamingRagService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<StreamingRagService> _logger;

    // Script chunk size for streaming (characters per chunk)
    private const int ScriptChunkSize = 500;

    public StreamingRagService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<StreamingRagService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async IAsyncEnumerable<RagStreamingEvent> ExplainStreamAsync(
        string gameId,
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Please provide a topic to explain.", "EMPTY_TOPIC"));
            yield break;
        }

        _logger.LogInformation("Starting streaming explain for game {GameId}, topic: {Topic}", gameId, topic);

        // Stream events, catching any exceptions
        var stream = ExplainStreamInternalAsync(gameId, topic, cancellationToken);

        // Use ConfigureAwaitOptions.None to avoid capturing context
        await foreach (var evt in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> ExplainStreamInternalAsync(
        string gameId,
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Step 1: Generate embedding
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating embeddings for topic..."));

        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(topic, cancellationToken);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate topic embedding: {Error}", embeddingResult.ErrorMessage);
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Unable to process topic.", "EMBEDDING_FAILED"));
            yield break;
        }

        var topicEmbedding = embeddingResult.Embeddings[0];

        // Step 2: Search Qdrant
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching vector database for relevant content..."));

        var searchResult = await _qdrantService.SearchAsync(gameId, topicEmbedding, limit: 5, cancellationToken);

        if (!searchResult.Success || searchResult.Results.Count == 0)
        {
            _logger.LogInformation("No vector results found for topic {Topic} in game {GameId}", topic, gameId);
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError($"No relevant information found about '{topic}' in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // Step 3: Emit citations
        var citations = searchResult.Results.Select(r => new Snippet(
            r.Text,
            $"PDF:{r.PdfId}",
            r.Page,
            0
        )).ToList();

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(citations));

        // Step 4: Build and emit outline
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Building outline structure..."));

        var outline = BuildOutline(topic, searchResult.Results);
        yield return CreateEvent(StreamingEventType.Outline,
            new StreamingOutline(outline));

        // Step 5: Build and stream script in chunks
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating explanation script..."));

        var script = BuildScript(topic, searchResult.Results);
        var scriptChunks = ChunkScript(script);

        for (int i = 0; i < scriptChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return CreateEvent(StreamingEventType.ScriptChunk,
                new StreamingScriptChunk(scriptChunks[i], i, scriptChunks.Count));

            // Small delay between chunks to simulate progressive generation
            // and avoid overwhelming the client
            if (i < scriptChunks.Count - 1)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        // Step 6: Calculate metadata and emit complete
        var wordCount = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var estimatedMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
        var confidence = searchResult.Results.Count > 0
            ? (double?)searchResult.Results.Max(r => r.Score)
            : null;

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                estimatedMinutes,
                0, // Token counts not available in non-LLM explain
                0,
                0,
                confidence));

        _logger.LogInformation(
            "Streaming explain completed for game {GameId}, topic: {Topic}, {ChunkCount} chunks sent",
            gameId, topic, scriptChunks.Count);
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }

    private ExplainOutline BuildOutline(string topic, IReadOnlyList<SearchResultItem> results)
    {
        var sections = new List<string>();

        for (int i = 0; i < results.Count && i < 5; i++)
        {
            var result = results[i];
            var text = result.Text.Trim();
            var firstSentence = text.Split('.')[0];
            if (firstSentence.Length > 60)
            {
                firstSentence = firstSentence.Substring(0, 57) + "...";
            }
            sections.Add(firstSentence);
        }

        return new ExplainOutline(topic, sections);
    }

    private string BuildScript(string topic, IReadOnlyList<SearchResultItem> results)
    {
        var scriptParts = new List<string>();

        scriptParts.Add($"# Explanation: {topic}");
        scriptParts.Add("");
        scriptParts.Add("## Overview");
        scriptParts.Add("");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            scriptParts.Add($"### Section {i + 1} (Page {result.Page})");
            scriptParts.Add("");
            scriptParts.Add(result.Text.Trim());
            scriptParts.Add("");
        }

        return string.Join("\n", scriptParts);
    }

    private List<string> ChunkScript(string script)
    {
        var chunks = new List<string>();

        // Split script into chunks of approximately ScriptChunkSize characters
        // Try to split at paragraph boundaries for better readability
        var paragraphs = script.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new List<string>();
        var currentChunkSize = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphSize = paragraph.Length + 2; // +2 for the "\n\n" separator

            if (currentChunkSize > 0 && currentChunkSize + paragraphSize > ScriptChunkSize)
            {
                // Current chunk is full, save it and start a new one
                chunks.Add(string.Join("\n\n", currentChunk));
                currentChunk.Clear();
                currentChunkSize = 0;
            }

            currentChunk.Add(paragraph);
            currentChunkSize += paragraphSize;
        }

        // Add the last chunk if not empty
        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join("\n\n", currentChunk));
        }

        // If no chunks were created (empty script), return a single empty chunk
        if (chunks.Count == 0)
        {
            chunks.Add("");
        }

        return chunks;
    }
}
