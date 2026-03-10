using System.Diagnostics;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for AskAgentQuestionCommand with 3 search strategies
/// POC: Agent default behavior with token/cost tracking
/// Issue #5580: Session-aware RAG chat — when GameSessionId is provided,
/// loads GameSessionContext, caches it, and filters by all game IDs.
/// </summary>
internal class AskAgentQuestionCommandHandler : IRequestHandler<AskAgentQuestionCommand, AgentChatResponse>
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILlmService _llmService;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IGameSessionOrchestratorService _sessionOrchestrator;
    private readonly IHybridCacheService _hybridCache;
    private readonly ILogger<AskAgentQuestionCommandHandler> _logger;

    /// <summary>Cache TTL for GameSessionContext.</summary>
    private static readonly TimeSpan SessionContextCacheTtl = TimeSpan.FromMinutes(5);

    public AskAgentQuestionCommandHandler(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        ILlmService llmService,
        ILlmCostLogRepository costLogRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IGameSessionOrchestratorService sessionOrchestrator,
        IHybridCacheService hybridCache,
        ILogger<AskAgentQuestionCommandHandler> logger)
    {
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentChatResponse> Handle(AskAgentQuestionCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Processing agent question with strategy={Strategy}, question={Question}",
            request.Strategy, request.Question);

        // Issue #5580: Session-aware RAG — load session context when GameSessionId is provided
        GameSessionContextDto? sessionContext = null;
        if (request.GameSessionId.HasValue)
        {
            var cacheKey = $"game-session-context:{request.GameSessionId.Value}";
            sessionContext = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async ct => await _sessionOrchestrator.BuildContextAsync(request.GameSessionId.Value, ct).ConfigureAwait(false),
                tags: new[] { $"session:{request.GameSessionId.Value}" },
                expiration: SessionContextCacheTtl,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Loaded session context for session {SessionId}: DegradationLevel={Level}, AllGameIds=[{GameIds}]",
                request.GameSessionId.Value,
                sessionContext.DegradationLevel,
                string.Join(",", sessionContext.AllGameIds));

            // If NoAI degradation, return informative message without hitting RAG
            if (sessionContext.DegradationLevel == SessionDegradationLevel.NoAI)
            {
                _logger.LogInformation(
                    "Session {SessionId} has NoAI degradation — returning informative message",
                    request.GameSessionId.Value);

                return new AgentChatResponse
                {
                    Strategy = request.Strategy,
                    StrategyDescription = GetStrategyDescription(request.Strategy),
                    Answer = SessionContextPromptBuilder.GetNoAiDegradationMessage(),
                    RetrievedChunks = new List<CodeChunkDto>(),
                    TokenUsage = new TokenUsageDto
                    {
                        PromptTokens = 0,
                        CompletionTokens = 0,
                        TotalTokens = 0,
                        EmbeddingTokens = 0
                    },
                    CostBreakdown = CostBreakdownDto.Zero(),
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        // Step 0: Check if documents are still being processed
        if (request.GameId.HasValue && sessionContext == null)
        {
            var documents = await _pdfDocumentRepository.FindByGameIdAsync(request.GameId.Value, cancellationToken).ConfigureAwait(false);
            if (documents.Count > 0)
            {
                var notCompleted = documents.Count(d =>
                    !string.Equals(d.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase));
                if (notCompleted > 0)
                {
                    _logger.LogInformation(
                        "Documents not ready for game {GameId}: {Processing}/{Total} still processing",
                        request.GameId, notCompleted, documents.Count);
                    throw new InvalidOperationException(
                        $"{notCompleted} of {documents.Count} documents are still being processed. Please wait for processing to complete before asking questions.");
                }
            }
        }

        // Initialize tracking
        var tokenUsage = new TokenUsageDto
        {
            PromptTokens = 0,
            CompletionTokens = 0,
            TotalTokens = 0,
            EmbeddingTokens = 0
        };
        var costBreakdown = CostBreakdownDto.Zero();

        // Step 1: Generate embedding for question (local, $0)
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Embedding generation failed: {Error}", embeddingResult.ErrorMessage);
            throw new InvalidOperationException($"Failed to generate embedding: {embeddingResult.ErrorMessage}");
        }

        // Track embedding tokens (estimate: ~word count / 0.75)
        tokenUsage = tokenUsage with
        {
            EmbeddingTokens = EstimateTokens(request.Question)
        };

        // Step 2: Vector search in Qdrant (local, $0)
        // Issue #5580: When session context exists, search across all game IDs (primary + expansions)
        var gameId = sessionContext != null && sessionContext.AllGameIds.Count > 0
            ? string.Join(",", sessionContext.AllGameIds)
            : request.GameId?.ToString() ?? "default";

        SearchResult searchResult;
        if (sessionContext != null && sessionContext.AllGameIds.Count > 0)
        {
            _logger.LogInformation(
                "Session-aware search across {GameCount} games: [{GameIds}]",
                sessionContext.AllGameIds.Count, gameId);

            // Use the first game ID for the Qdrant search (IQdrantService only supports single gameId string)
            // For true multi-game search we search each game and merge results
            var allResults = new List<SearchResultItem>();
            foreach (var gid in sessionContext.AllGameIds)
            {
                var partialResult = await _qdrantService.SearchAsync(
                    gid.ToString(),
                    embeddingResult.Embeddings[0],
                    request.TopK,
                    documentIds: null,
                    cancellationToken).ConfigureAwait(false);

                if (partialResult.Success)
                {
                    allResults.AddRange(partialResult.Results);
                }
            }

            // Sort by score and take top K
            var topResults = allResults
                .OrderByDescending(r => r.Score)
                .Take(request.TopK)
                .ToList();

            searchResult = new SearchResult
            {
                Success = true,
                Results = topResults
            };
        }
        else
        {
            var singleGameId = request.GameId?.ToString() ?? "default";
            searchResult = await _qdrantService.SearchAsync(
                singleGameId,
                embeddingResult.Embeddings[0],
                request.TopK,
                documentIds: null,
                cancellationToken).ConfigureAwait(false);
        }

        if (!searchResult.Success)
        {
            _logger.LogError("Vector search failed: {Error}", searchResult.ErrorMessage);
            throw new InvalidOperationException($"Vector search failed: {searchResult.ErrorMessage}");
        }

        // Filter by min score
        var filteredChunks = searchResult.Results
            .Where(r => r.Score >= request.MinScore)
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} chunks with score >= {MinScore}",
            filteredChunks.Count, request.MinScore);

        // Convert to DTOs
        var chunkDtos = filteredChunks.Select(chunk => new CodeChunkDto
        {
            FilePath = chunk.PdfId,
            StartLine = chunk.Page,
            EndLine = chunk.Page,
            CodePreview = TruncateText(chunk.Text, 500),
            RelevanceScore = chunk.Score,
            BoundedContext = gameId,
            ChunkIndex = chunk.ChunkIndex
        }).ToList();

        // Step 3: Strategy-based generation
        string? answer = null;
        string provider = "Local";
        string? modelUsed = null;

        switch (request.Strategy)
        {
            case AgentSearchStrategy.RetrievalOnly:
                // No LLM generation, return chunks only
                _logger.LogInformation("RetrievalOnly: Skipping LLM generation");
                break;

            case AgentSearchStrategy.SingleModel:
                // Single LLM call with cost-optimized routing (80% Ollama free)
                var singleResult = await GenerateSingleModelAnswer(
                    request.Question,
                    filteredChunks,
                    sessionContext,
                    cancellationToken).ConfigureAwait(false);

                answer = singleResult.Answer;
                tokenUsage = tokenUsage with
                {
                    PromptTokens = singleResult.PromptTokens,
                    CompletionTokens = singleResult.CompletionTokens
                };
                costBreakdown = costBreakdown with
                {
                    LlmCost = singleResult.Cost,
                    TotalCost = singleResult.Cost,
                    Provider = singleResult.Provider,
                    ModelUsed = singleResult.Model
                };
                provider = singleResult.Provider;
                modelUsed = singleResult.Model;
                break;

            case AgentSearchStrategy.MultiModelConsensus:
                // Multi-model consensus (GPT-4 + Claude)
                var consensusResult = await GenerateMultiModelConsensus(
                    request.Question,
                    filteredChunks,
                    sessionContext,
                    cancellationToken).ConfigureAwait(false);

                answer = consensusResult.Answer;
                tokenUsage = tokenUsage with
                {
                    PromptTokens = consensusResult.PromptTokens,
                    CompletionTokens = consensusResult.CompletionTokens
                };
                costBreakdown = costBreakdown with
                {
                    LlmCost = consensusResult.Cost,
                    TotalCost = consensusResult.Cost,
                    Provider = "Multi-Model (GPT-4 + Claude)",
                    ModelUsed = consensusResult.Model
                };
                provider = "Multi-Model";
                modelUsed = consensusResult.Model;
                break;
        }

        // Calculate total tokens
        tokenUsage = tokenUsage with
        {
            TotalTokens = tokenUsage.PromptTokens + tokenUsage.CompletionTokens + tokenUsage.EmbeddingTokens
        };

        // Step 4: Persist cost log to database
        await PersistCostLog(
            sessionId,
            request.Strategy,
            tokenUsage,
            costBreakdown,
            provider,
            modelUsed,
            cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogInformation(
            "Agent question completed: strategy={Strategy}, tokens={Tokens}, cost=${Cost}, latency={Latency}ms",
            request.Strategy, tokenUsage.TotalTokens, costBreakdown.TotalCost, stopwatch.ElapsedMilliseconds);

        return new AgentChatResponse
        {
            Strategy = request.Strategy,
            StrategyDescription = GetStrategyDescription(request.Strategy),
            Answer = answer,
            RetrievedChunks = chunkDtos,
            TokenUsage = tokenUsage,
            CostBreakdown = costBreakdown,
            LatencyMs = (int)stopwatch.ElapsedMilliseconds,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<(string Answer, int PromptTokens, int CompletionTokens, decimal Cost, string Provider, string Model)>
        GenerateSingleModelAnswer(string question, List<SearchResultItem> chunks, GameSessionContextDto? sessionContext, CancellationToken cancellationToken)
    {
        var context = BuildContextFromChunks(chunks);
        var systemPrompt = "You are a helpful assistant that answers questions based on the provided context. " +
                          "If the context doesn't contain relevant information, say so clearly.";

        // Issue #5580: Inject session context preamble
        if (sessionContext != null)
        {
            systemPrompt = SessionContextPromptBuilder.BuildSessionPreamble(sessionContext) + systemPrompt;
        }

        var userPrompt = $"Context:\n{context}\n\nQuestion: {question}\n\nProvide a concise answer based on the context.";

        var result = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            RequestSource.Manual,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("LLM generation failed: {Error}", result.ErrorMessage);
            throw new InvalidOperationException($"LLM generation failed: {result.ErrorMessage}");
        }

        // Extract usage from LlmCompletionResult
        var promptTokens = EstimateTokens(systemPrompt + userPrompt);
        var completionTokens = EstimateTokens(result.Response);

        return (
            Answer: result.Response,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            Cost: 0m, // Assume Ollama free tier for POC
            Provider: "Ollama",
            Model: "llama-3.3-70b-versatile"
        );
    }

    private async Task<(string Answer, int PromptTokens, int CompletionTokens, decimal Cost, string Model)>
        GenerateMultiModelConsensus(string question, List<SearchResultItem> chunks, GameSessionContextDto? sessionContext, CancellationToken cancellationToken)
    {
        var context = BuildContextFromChunks(chunks);
        var systemPrompt = "You are a helpful assistant that answers questions based on the provided context.";

        // Issue #5580: Inject session context preamble
        if (sessionContext != null)
        {
            systemPrompt = SessionContextPromptBuilder.BuildSessionPreamble(sessionContext) + systemPrompt;
        }

        var userPrompt = $"Context:\n{context}\n\nQuestion: {question}\n\nProvide a detailed answer.";

        // Call GPT-4 (via OpenRouter)
        var gpt4Result = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            RequestSource.Manual,
            cancellationToken).ConfigureAwait(false);

        // Call Claude (via OpenRouter) - simplified for POC, reuse same service
        var claudeResult = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            RequestSource.Manual,
            cancellationToken).ConfigureAwait(false);

        // Simple consensus: concatenate both answers
        var consensusAnswer = $"GPT-4 Response:\n{gpt4Result.Response}\n\nClaude Response:\n{claudeResult.Response}";

        var totalPromptTokens = 2 * EstimateTokens(systemPrompt + userPrompt);
        var totalCompletionTokens = EstimateTokens(gpt4Result.Response) + EstimateTokens(claudeResult.Response);
        var totalCost = 0.027m; // Estimated cost for GPT-4 + Claude

        return (
            Answer: consensusAnswer,
            PromptTokens: totalPromptTokens,
            CompletionTokens: totalCompletionTokens,
            Cost: totalCost,
            Model: "gpt-4 + claude-3.5-sonnet"
        );
    }

    private async Task PersistCostLog(
        string sessionId,
        AgentSearchStrategy strategy,
        TokenUsageDto tokenUsage,
        CostBreakdownDto costBreakdown,
        string provider,
        string? model,
        CancellationToken cancellationToken)
    {
        try
        {
            var cost = new LlmCostCalculation
            {
                ModelId = model ?? "unknown",
                Provider = provider,
                PromptTokens = tokenUsage.PromptTokens,
                CompletionTokens = tokenUsage.CompletionTokens,
                InputCost = 0m, // Calculated by service
                OutputCost = costBreakdown.LlmCost
            };

            await _costLogRepository.LogCostAsync(
                userId: null,
                userRole: "POC-Test",
                cost: cost,
                endpoint: $"/api/v1/agents/chat/ask?strategy={strategy}",
                success: true,
                errorMessage: null,
                latencyMs: 0, // Set by caller
                ipAddress: null,
                userAgent: null,
                source: RequestSource.Manual,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Cost log persisted for strategy={Strategy}", strategy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist cost log");
            // Don't throw - cost logging failure shouldn't break the request
        }
    }

    private static string BuildContextFromChunks(List<SearchResultItem> chunks)
    {
        var contextParts = chunks.Select((chunk, index) =>
            $"[{index + 1}] (Score: {chunk.Score:F2}, Page: {chunk.Page})\n{chunk.Text}");

        return string.Join("\n\n---\n\n", contextParts);
    }

    private static string GetStrategyDescription(AgentSearchStrategy strategy)
    {
        return strategy switch
        {
            AgentSearchStrategy.RetrievalOnly => "Retrieval-Only: No LLM generation, raw chunks ($0, ~300ms)",
            AgentSearchStrategy.SingleModel => "Single Model: RAG + LLM synthesis (80% Ollama free, ~2-5s)",
            AgentSearchStrategy.MultiModelConsensus => "Multi-Model Consensus: GPT-4 + Claude validation (~$0.027, ~5-10s)",
            _ => "Unknown strategy"
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~0.75 tokens per word (OpenAI standard)
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)Math.Ceiling(wordCount / 0.75);
    }
}
