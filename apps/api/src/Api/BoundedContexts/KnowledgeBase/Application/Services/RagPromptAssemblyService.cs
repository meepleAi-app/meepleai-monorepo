using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates the full RAG prompt assembly pipeline.
/// Phase 0: embedding → Qdrant search → prompt assembly.
/// Phase 1: + reranking, query expansion, enhanced confidence scoring.
/// </summary>
internal sealed class RagPromptAssemblyService : IRagPromptAssemblyService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ICrossEncoderReranker _reranker;
    private readonly ILlmService _llmService;
    private readonly ILogger<RagPromptAssemblyService> _logger;

    // RAG search parameters
    private const int DefaultTopK = 15; // Increased from 10 to feed reranker more candidates
    private const int RerankedTopK = 5;  // Final chunk count after reranking
    private const float DefaultMinScore = 0.55f;

    // Chat history thresholds
    private const int HistoryThreshold = 10;
    internal const int RecentMessageCount = 5;

    // Confidence scoring
    private const float HighScoreThreshold = 0.8f;
    private const float ConfidencePenalty = 0.1f;
    private static readonly string[] HedgeWords = [
        "forse", "probabilmente", "non sono sicuro", "potrebbe essere",
        "i'm not sure", "i think", "maybe", "perhaps", "possibly",
        "not certain", "it might", "it could"
    ];

    // Query expansion
    private const string QueryExpansionSystemPrompt =
        "You are a query expansion assistant for board game documentation search. " +
        "Given a user question, generate 2-3 alternative phrasings that might match game rules documentation. " +
        "Return ONLY a JSON array of strings, no other text. Example: [\"alt1\", \"alt2\"]";

    public RagPromptAssemblyService(
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ICrossEncoderReranker reranker,
        ILlmService llmService,
        ILogger<RagPromptAssemblyService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AssembledPrompt> AssemblePromptAsync(
        string agentTypology,
        string gameTitle,
        GameState? gameState,
        string userQuestion,
        Guid gameId,
        ChatThread? chatThread,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agentTypology);
        ArgumentNullException.ThrowIfNull(gameTitle);
        ArgumentNullException.ThrowIfNull(userQuestion);

        // Step 1: Retrieve RAG context
        var (ragContext, citations) = await RetrieveRagContextAsync(userQuestion, gameId, ct).ConfigureAwait(false);

        // Step 2: Build system prompt (persona + RAG chunks)
        var systemPrompt = BuildSystemPrompt(agentTypology, gameTitle, gameState, ragContext);

        // Step 3: Build user prompt (chat history + current question)
        var userPrompt = BuildUserPrompt(userQuestion, chatThread);

        // Step 4: Estimate tokens
        var estimatedTokens = EstimateTokens(systemPrompt) + EstimateTokens(userPrompt);

        _logger.LogInformation(
            "Assembled prompt: {AgentType} for {Game}, {ChunkCount} RAG chunks, {HistoryCount} history messages, ~{Tokens} tokens",
            agentTypology, gameTitle, citations.Count,
            chatThread?.Messages.Count(m => !m.IsDeleted && !m.IsInvalidated) ?? 0,
            estimatedTokens);

        return new AssembledPrompt(systemPrompt, userPrompt, citations, estimatedTokens);
    }

    private async Task<(string ragContext, List<ChunkCitation> citations)> RetrieveRagContextAsync(
        string userQuestion, Guid gameId, CancellationToken ct)
    {
        var citations = new List<ChunkCitation>();

        try
        {
            // Step 1: Query expansion (optional, graceful degradation)
            var queries = await ExpandQueryAsync(userQuestion, ct).ConfigureAwait(false);

            // Step 2: Generate embeddings for all queries
            var allChunks = new List<SearchResultItem>();
            foreach (var query in queries)
            {
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, ct).ConfigureAwait(false);
                if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
                {
                    if (string.Equals(query, userQuestion, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("Embedding generation failed for primary query: {Error}", embeddingResult.ErrorMessage);
                        return (string.Empty, citations);
                    }
                    continue; // Skip failed expansion queries
                }

                var searchResult = await _qdrantService.SearchAsync(
                    gameId.ToString(),
                    embeddingResult.Embeddings[0],
                    DefaultTopK,
                    documentIds: null,
                    ct).ConfigureAwait(false);

                if (searchResult.Success)
                {
                    allChunks.AddRange(searchResult.Results);
                }
            }

            // Deduplicate by PdfId+ChunkIndex, keep highest score
            var filteredChunks = allChunks
                .GroupBy(r => $"{r.PdfId}:{r.ChunkIndex}", StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .Where(r => r.Score >= DefaultMinScore)
                .OrderByDescending(r => r.Score)
                .ToList();

            if (filteredChunks.Count == 0)
            {
                _logger.LogInformation("No chunks above minScore {MinScore} for game {GameId}", DefaultMinScore, gameId);
                return (string.Empty, citations);
            }

            _logger.LogInformation("Retrieved {Count} relevant chunks from {QueryCount} queries (scores: {MinScore:F2}-{MaxScore:F2})",
                filteredChunks.Count, queries.Count,
                filteredChunks[^1].Score,
                filteredChunks[0].Score);

            // Step 3: Rerank if available (graceful degradation)
            filteredChunks = await TryRerankAsync(userQuestion, filteredChunks, ct).ConfigureAwait(false);

            // Format chunks and track citations
            var sb = new StringBuilder();
            foreach (var chunk in filteredChunks)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"[Source: Document {chunk.PdfId}, Page {chunk.Page}, Relevance: {chunk.Score:F2}]");
                sb.AppendLine(chunk.Text);
                sb.AppendLine("---");

                citations.Add(new ChunkCitation(
                    DocumentId: chunk.PdfId,
                    PageNumber: chunk.Page,
                    RelevanceScore: chunk.Score,
                    SnippetPreview: chunk.Text.Length > 120 ? string.Concat(chunk.Text.AsSpan(0, 117), "...") : chunk.Text));
            }

            return (sb.ToString().TrimEnd(), citations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed for game {GameId}", gameId);
            return (string.Empty, citations);
        }
    }

    private async Task<List<string>> ExpandQueryAsync(string userQuestion, CancellationToken ct)
    {
        var queries = new List<string> { userQuestion }; // Always include original

        try
        {
            var result = await _llmService.GenerateCompletionAsync(
                QueryExpansionSystemPrompt,
                userQuestion,
                RequestSource.RagPipeline,
                ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
            {
                var expansions = JsonSerializer.Deserialize<List<string>>(result.Response.Trim());
                if (expansions != null)
                {
                    queries.AddRange(expansions.Where(e => !string.IsNullOrWhiteSpace(e)).Take(3));
                    _logger.LogDebug("Query expanded: original + {ExpansionCount} alternatives", expansions.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Query expansion failed, using original query only");
        }

        return queries;
    }

    private async Task<List<SearchResultItem>> TryRerankAsync(
        string userQuestion, List<SearchResultItem> chunks, CancellationToken ct)
    {
        if (chunks.Count <= RerankedTopK)
            return chunks.Take(RerankedTopK).ToList();

        try
        {
            var rerankChunks = chunks.Select(c => new RerankChunk(
                Id: $"{c.PdfId}:{c.ChunkIndex}",
                Content: c.Text,
                OriginalScore: c.Score
            )).ToList();

            var result = await _reranker.RerankAsync(userQuestion, rerankChunks, RerankedTopK, ct).ConfigureAwait(false);

            _logger.LogInformation("Reranked {Input} → {Output} chunks in {TimeMs:F1}ms",
                chunks.Count, result.Chunks.Count, result.ProcessingTimeMs);

            // Map reranked results back to SearchResultItems, updating scores
            var rerankedLookup = result.Chunks.ToDictionary(r => r.Id, StringComparer.Ordinal);
            return chunks
                .Where(c => rerankedLookup.ContainsKey($"{c.PdfId}:{c.ChunkIndex}"))
                .OrderByDescending(c => rerankedLookup[$"{c.PdfId}:{c.ChunkIndex}"].RerankScore)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranker failed, using raw Qdrant scores (top {TopK})", RerankedTopK);
            return chunks.Take(RerankedTopK).ToList();
        }
    }

    /// <summary>
    /// Computes confidence score based on RAG chunk quality and response content.
    /// </summary>
    internal static double? ComputeConfidence(List<ChunkCitation> citations, string responseText)
    {
        if (citations.Count == 0)
            return null;

        // Base: average relevance score
        var confidence = (double)citations.Average(c => c.RelevanceScore);

        // Penalty: no chunks above high score threshold
        if (!citations.Any(c => c.RelevanceScore >= HighScoreThreshold))
            confidence -= ConfidencePenalty;

        // Penalty: hedge words in response
        var lowerResponse = responseText.ToLowerInvariant();
        if (HedgeWords.Any(hw => lowerResponse.Contains(hw, StringComparison.Ordinal)))
            confidence -= ConfidencePenalty;

        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    private static string BuildSystemPrompt(string agentTypology, string gameTitle, GameState? gameState, string ragContext)
    {
        var sb = new StringBuilder();

        // Persona
        sb.AppendLine(CultureInfo.InvariantCulture, $"You are a {agentTypology} assistant for the board game \"{gameTitle}\".");
        sb.AppendLine("Answer questions accurately based on the game rules and documentation provided below.");
        sb.AppendLine("If the provided context does not contain enough information to answer, say so clearly.");
        sb.AppendLine("Always refer to specific rules when possible.");
        sb.AppendLine();

        // RAG context
        if (!string.IsNullOrWhiteSpace(ragContext))
        {
            sb.AppendLine("## Game Rules and Documentation");
            sb.AppendLine();
            sb.AppendLine(ragContext);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Note: No game documentation is currently available. Answer based on general knowledge and clearly indicate this limitation.");
            sb.AppendLine();
        }

        // Game state (if active session)
        if (gameState != null)
        {
            sb.AppendLine("## Current Game State");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Turn: {gameState.CurrentTurn}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Active player: {gameState.ActivePlayer}");

            if (gameState.PlayerScores.Count > 0)
            {
                var scores = string.Join(", ", gameState.PlayerScores.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Scores: {scores}");
            }

            if (!string.IsNullOrWhiteSpace(gameState.GamePhase))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Phase: {gameState.GamePhase}");

            if (!string.IsNullOrWhiteSpace(gameState.LastAction))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Last action: {gameState.LastAction}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildUserPrompt(string userQuestion, ChatThread? chatThread)
    {
        var sb = new StringBuilder();

        // Chat history
        if (chatThread != null)
        {
            var activeMessages = chatThread.Messages
                .Where(m => !m.IsDeleted && !m.IsInvalidated)
                .OrderBy(m => m.SequenceNumber)
                .ToList();

            if (activeMessages.Count > 0)
            {
                sb.AppendLine("## Conversation History");
                sb.AppendLine();

                if (activeMessages.Count <= HistoryThreshold)
                {
                    // Include all messages
                    foreach (var msg in activeMessages)
                    {
                        var role = msg.IsUserMessage ? "User" : "Assistant";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{role}: {msg.Content}");
                    }
                }
                else
                {
                    // Summary + recent messages
                    if (!string.IsNullOrWhiteSpace(chatThread.ConversationSummary))
                    {
                        sb.AppendLine("[Previous conversation summary]");
                        sb.AppendLine(chatThread.ConversationSummary);
                        sb.AppendLine();
                    }

                    sb.AppendLine("[Recent messages]");
                    var recentMessages = activeMessages.TakeLast(RecentMessageCount);
                    foreach (var msg in recentMessages)
                    {
                        var role = msg.IsUserMessage ? "User" : "Assistant";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{role}: {msg.Content}");
                    }
                }

                sb.AppendLine();
            }
        }

        // Current question
        sb.AppendLine("## Current Question");
        sb.AppendLine();
        sb.AppendLine(userQuestion);

        return sb.ToString();
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token (GPT-style)
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
