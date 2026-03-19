using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for StreamExplainQuery.
/// Implements streaming RAG explain flow with progressive SSE events.
/// API-02: Emits StateUpdate → Citations → Outline → ScriptChunk(s) → Complete
/// </summary>
internal class StreamExplainQueryHandler : IStreamingQueryHandler<StreamExplainQuery, RagStreamingEvent>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<StreamExplainQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    // Script chunk size for streaming (characters per chunk)
    private const int ScriptChunkSize = 500;

    // Word delimiters for reading time calculation (CA1861)
    private static readonly char[] WordDelimiters = { ' ', '\n', '\r', '\t' };

    public StreamExplainQueryHandler(
        IEmbeddingService embeddingService,
        ILogger<StreamExplainQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamExplainQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Topic))
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Please provide a topic to explain.", "EMPTY_TOPIC"));
            yield break;
        }

        _logger.LogInformation("Starting streaming explain for game {GameId}, topic: {Topic}",
            query.GameId, query.Topic);

        // Step 1: Generate embedding for topic
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating embeddings for topic..."));

        var topicEmbedding = await GenerateTopicEmbeddingAsync(query.Topic, cancellationToken).ConfigureAwait(false);
        if (topicEmbedding == null)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Unable to process topic.", "EMBEDDING_FAILED"));
            yield break;
        }

        // Step 2: Search vector database
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching vector database for relevant content..."));

        var (searchResults, citations) = await SearchForTopicContentAsync(
            query.GameId, query.Topic, topicEmbedding, cancellationToken).ConfigureAwait(false);
        if (citations == null)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError($"No relevant information found about '{query.Topic}' in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(citations));

        // Step 3: Build outline
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Building outline structure..."));

        var (outline, script) = BuildOutlineAndScript(query.Topic, searchResults!);
        yield return CreateEvent(StreamingEventType.Outline,
            new StreamingOutline(outline));

        // Step 4: Stream script in chunks
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating explanation script..."));

        var scriptChunks = ChunkScript(script);

        for (int i = 0; i < scriptChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return CreateEvent(StreamingEventType.ScriptChunk,
                new StreamingScriptChunk(scriptChunks[i], i, scriptChunks.Count));

            // Small delay between chunks
            if (i < scriptChunks.Count - 1)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        // Step 5: Emit completion with calculated metrics
        var wordCount = script.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries).Length;
        var estimatedMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0)); // 200 words/min reading speed
        var maxConfidence = searchResults!.Max(r => r.Score);

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(estimatedMinutes, 0, 0, 0, maxConfidence));

        _logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}",
            query.GameId, query.Topic);
    }

    /// <summary>
    /// Generates embedding for topic with error handling.
    /// Returns embedding vector or null if generation failed.
    /// </summary>
    private async Task<float[]?> GenerateTopicEmbeddingAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(topic, cancellationToken).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate topic embedding: {Error}", embeddingResult.ErrorMessage);
            return null;
        }

        return embeddingResult.Embeddings[0];
    }

    /// <summary>
    /// Searches for topic content and builds citations.
    /// Returns (searchResults, citations) or (null, null) if no results found.
    /// NOTE: Qdrant dependency removed — always returns null (no results).
    /// </summary>
    private Task<(IReadOnlyList<SearchResultItem>? searchResults, List<Snippet>? citations)> SearchForTopicContentAsync(
        string gameId,
        string topic,
        float[] topicEmbedding,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("No vector results for topic {Topic} in game {GameId} — Qdrant removed",
            topic, gameId);
        return Task.FromResult<(IReadOnlyList<SearchResultItem>?, List<Snippet>?)>((null, null));
    }

    /// <summary>
    /// Builds outline and script from search results.
    /// Returns (outline, script).
    /// </summary>
    private (ExplainOutline outline, string script) BuildOutlineAndScript(
        string topic,
        IReadOnlyList<SearchResultItem> searchResults)
    {
        var outline = BuildOutline(topic, searchResults);
        var script = BuildScript(topic, searchResults);

        return (outline, script);
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
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
                firstSentence = string.Concat(firstSentence.AsSpan(0, 57), "...");
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
