using System.Globalization;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates the full RAG prompt assembly pipeline.
/// Replaces AgentPromptBuilder with RAG context, chat history, and token budget management.
/// </summary>
internal sealed class RagPromptAssemblyService : IRagPromptAssemblyService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<RagPromptAssemblyService> _logger;

    // RAG search parameters
    private const int DefaultTopK = 10;
    private const float DefaultMinScore = 0.55f;

    // Chat history thresholds
    private const int HistoryThreshold = 10;
    internal const int RecentMessageCount = 5;

    public RagPromptAssemblyService(
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<RagPromptAssemblyService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
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
            // Generate embedding
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(userQuestion, ct).ConfigureAwait(false);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogWarning("Embedding generation failed: {Error}", embeddingResult.ErrorMessage);
                return (string.Empty, citations);
            }

            // Search Qdrant
            var searchResult = await _qdrantService.SearchAsync(
                gameId.ToString(),
                embeddingResult.Embeddings[0],
                DefaultTopK,
                documentIds: null,
                ct).ConfigureAwait(false);

            if (!searchResult.Success)
            {
                _logger.LogWarning("Qdrant search failed: {Error}", searchResult.ErrorMessage);
                return (string.Empty, citations);
            }

            // Filter by minimum score
            var filteredChunks = searchResult.Results
                .Where(r => r.Score >= DefaultMinScore)
                .OrderByDescending(r => r.Score)
                .ToList();

            if (filteredChunks.Count == 0)
            {
                _logger.LogInformation("No chunks above minScore {MinScore} for game {GameId}", DefaultMinScore, gameId);
                return (string.Empty, citations);
            }

            _logger.LogInformation("Retrieved {Count} relevant chunks (scores: {MinScore:F2}-{MaxScore:F2})",
                filteredChunks.Count,
                filteredChunks[^1].Score,
                filteredChunks[0].Score);

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
