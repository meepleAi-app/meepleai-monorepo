using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for SendAgentMessageCommand.
/// Implements SSE streaming for standalone agent chat (non-session).
/// Issue #4126: API Integration for Agent Chat
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// </summary>
internal sealed class SendAgentMessageCommandHandler : IStreamingQueryHandler<SendAgentMessageCommand, RagStreamingEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUserBudgetService _userBudgetService;
    private readonly ILlmModelOverrideService _modelOverrideService;
    private readonly IModelConfigurationService _modelConfigService;
    private readonly ILogger<SendAgentMessageCommandHandler> _logger;

    public SendAgentMessageCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        MeepleAiDbContext dbContext,
        IUserBudgetService userBudgetService,
        ILlmModelOverrideService modelOverrideService,
        IModelConfigurationService modelConfigService,
        ILogger<SendAgentMessageCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userBudgetService = userBudgetService ?? throw new ArgumentNullException(nameof(userBudgetService));
        _modelOverrideService = modelOverrideService ?? throw new ArgumentNullException(nameof(modelOverrideService));
        _modelConfigService = modelConfigService ?? throw new ArgumentNullException(nameof(modelConfigService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        SendAgentMessageCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(command);

        await foreach (var @event in HandleCore(command, cancellationToken).ConfigureAwait(false))
        {
            yield return @event;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> HandleCore(
        SendAgentMessageCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting agent chat for Agent {AgentId}, User {UserId}, Thread {ChatThreadId}",
            command.AgentId, command.UserId, command.ChatThreadId);

        // Validate Agent exists
        var agent = await _agentRepository
            .GetByIdAsync(command.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"Agent {command.AgentId} not found", "AGENT_NOT_FOUND"));
            yield break;
        }

        // Load agent configuration and validate KB readiness
        var agentConfig = await _dbContext.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == command.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        if (agentConfig == null)
        {
            // Defense-in-depth: auto-create default config for orphaned agents
            _logger.LogWarning(
                "Agent {AgentId} has no current configuration — attempting auto-creation",
                command.AgentId);

            var autoDocIds = agent.GameId.HasValue
                ? await _dbContext.VectorDocuments
                    .Where(vd => vd.GameId == agent.GameId.Value && vd.IndexingStatus == "completed")
                    .Select(vd => vd.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : new List<Guid>();

            if (autoDocIds.Count == 0)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError(
                        "Agent non configurato. Configura l'agente prima di chattare.",
                        "AGENT_NOT_CONFIGURED"));
                yield break;
            }

            agentConfig = new AgentConfigurationEntity
            {
                Id = Guid.NewGuid(),
                AgentId = command.AgentId,
                LlmProvider = AgentDefaults.DefaultLlmProvider,
                LlmModel = AgentDefaults.DefaultFreeModel,
                AgentMode = 0,
                SelectedDocumentIdsJson = JsonSerializer.Serialize(autoDocIds),
                Temperature = AgentDefaults.DefaultTemperature,
                MaxTokens = AgentDefaults.DefaultMaxTokens,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = command.UserId
            };

            var configInsertFailed = false;
            try
            {
                _dbContext.Set<AgentConfigurationEntity>().Add(agentConfig);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Auto-created default config for agent {AgentId} with {DocCount} documents",
                    command.AgentId, autoDocIds.Count);
            }
            catch (DbUpdateException ex)
            {
                // Race condition: another request already created the config.
                _logger.LogWarning(ex,
                    "Concurrent config creation for agent {AgentId} — will re-read",
                    command.AgentId);
                _dbContext.Entry(agentConfig).State = EntityState.Detached;
                configInsertFailed = true;
            }

            if (configInsertFailed)
            {
                agentConfig = await _dbContext.AgentConfigurations
                    .FirstOrDefaultAsync(
                        c => c.AgentId == command.AgentId && c.IsCurrent,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (agentConfig == null)
                {
                    yield return CreateEvent(
                        StreamingEventType.Error,
                        new StreamingError(
                            "Agent non configurato. Configura l'agente prima di chattare.",
                            "AGENT_NOT_CONFIGURED"));
                    yield break;
                }
            }
        }

        // Parse and validate selected documents
        var selectedDocumentIds = new List<Guid>();
        if (!string.IsNullOrEmpty(agentConfig.SelectedDocumentIdsJson))
        {
            try
            {
                selectedDocumentIds = JsonSerializer.Deserialize<List<Guid>>(agentConfig.SelectedDocumentIdsJson)
                    ?? new List<Guid>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse document IDs for agent {AgentId}", command.AgentId);
            }
        }

        if (selectedDocumentIds.Count == 0)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    "Agent non ha documenti nella Knowledge Base. Aggiungi documenti per abilitare la chat.",
                    "AGENT_NO_DOCUMENTS"));
            yield break;
        }

        _logger.LogInformation(
            "Agent {AgentId} has {DocumentCount} documents in KB",
            command.AgentId, selectedDocumentIds.Count);

        // Resolve or create ChatThread
        ChatThread? thread = null;
        if (command.ChatThreadId.HasValue)
        {
            thread = await _chatThreadRepository
                .GetByIdAsync(command.ChatThreadId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (thread == null)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError($"ChatThread {command.ChatThreadId.Value} not found", "THREAD_NOT_FOUND"));
                yield break;
            }
        }
        else
        {
            // Auto-create thread for new conversations
            thread = new ChatThread(
                id: Guid.NewGuid(),
                userId: command.UserId,
                agentId: command.AgentId,
                agentType: agent.Type.Value,
                title: command.UserQuestion.Length > 100
                    ? string.Concat(command.UserQuestion.AsSpan(0, 97), "...")
                    : command.UserQuestion);

            await _chatThreadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Persist user message
        thread.AddUserMessage(command.UserQuestion);
        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // State update (include ThreadId for frontend to track)
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Starting chat with {agent.Name}", thread.Id));

        // RAG Pipeline: Retrieve relevant context from Knowledge Base
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base...", thread.Id));

        // Step 1: Generate embedding for user question
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
            command.UserQuestion,
            cancellationToken).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Embedding generation failed for agent {AgentId}", command.AgentId);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("Failed to generate embedding for query", "EMBEDDING_FAILED"));
            yield break;
        }

        // Step 2: Vector search in Qdrant with document filtering
        // POC FIX #1: Get gameId from VectorDocument (thread.GameId is null for new chats)
        // POC FIX #2: Get PdfDocumentIds (Qdrant uses pdf_id, not vector_document_id)
        Guid? gameIdForSearch = thread.GameId;
        var pdfDocumentIds = new List<string>();

        if (selectedDocumentIds.Count > 0)
        {
            var vectorDocs = await _dbContext.VectorDocuments
                .Where(vd => selectedDocumentIds.Contains(vd.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!gameIdForSearch.HasValue && vectorDocs.Count > 0)
            {
                gameIdForSearch = vectorDocs[0].GameId;
            }

            // Qdrant stores pdf_id without hyphens (from BlobStorageService.FileId format "N")
            pdfDocumentIds = vectorDocs.Select(vd => vd.PdfDocumentId.ToString("N")).ToList();
        }

        var gameId = gameIdForSearch?.ToString() ?? "default";

        _logger.LogInformation(
            "Searching for game {GameId} with {DocumentCount} PDF document filters",
            gameId, pdfDocumentIds.Count);

        // Debug: emit retrieval start event (Issue #4916)
        var retrievalStopwatch = Stopwatch.StartNew();
        yield return CreateEvent(
            StreamingEventType.DebugRetrievalStart,
            new
            {
                query = command.UserQuestion,
                gameId,
                documentIds = pdfDocumentIds,
                limit = 10,
                minScore = 0.6
            });

        var searchResult = await _qdrantService.SearchAsync(
            gameId,
            embeddingResult.Embeddings[0],
            limit: 10,
            documentIds: pdfDocumentIds, // Use PdfDocumentIds, not VectorDocument IDs
            cancellationToken).ConfigureAwait(false);

        if (!searchResult.Success)
        {
            _logger.LogError("Vector search failed for agent {AgentId}: {Error}",
                command.AgentId, searchResult.ErrorMessage);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"Vector search failed: {searchResult.ErrorMessage}", "SEARCH_FAILED"));
            yield break;
        }

        // Step 3: Filter results by minimum score and build context
        var minScore = 0.6; // Configurable threshold
        var relevantChunks = searchResult.Results
            .Where(r => r.Score >= minScore)
            .ToList();
        retrievalStopwatch.Stop();

        _logger.LogInformation(
            "Retrieved {ChunkCount} relevant chunks (score >= {MinScore}) for agent {AgentId}",
            relevantChunks.Count, minScore, command.AgentId);

        // Debug: emit retrieval results event (Issue #4916)
        yield return CreateEvent(
            StreamingEventType.DebugRetrievalResults,
            new
            {
                totalResults = searchResult.Results.Count,
                filteredCount = relevantChunks.Count,
                minScore,
                latencyMs = retrievalStopwatch.ElapsedMilliseconds,
                chunks = relevantChunks.Take(5).Select(c => new
                {
                    text = c.Text.Length > 200 ? string.Concat(c.Text.AsSpan(0, 197), "...") : c.Text,
                    score = c.Score,
                    page = c.Page,
                    pdfId = c.PdfId
                }).ToList()
            });

        var ragContext = BuildContextFromChunks(relevantChunks);

        // Compute confidence from average top-3 retrieval scores (null if no chunks retrieved)
        var retrievalConfidence = relevantChunks.Count > 0
            ? (double?)relevantChunks.Take(3).Average(c => c.Score)
            : null;

        // Step 4: Emit citations for frontend display
        if (relevantChunks.Count > 0)
        {
            var citations = relevantChunks.Select(c => new
            {
                documentId = c.PdfId,
                pageNumber = c.Page,
                score = c.Score
            }).ToList();

            yield return CreateEvent(
                StreamingEventType.Citations,
                citations);
        }

        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating response...", thread.Id));

        // Prepare prompts for LLM
        var systemPrompt = $"You are {agent.Name}, a specialized board game AI assistant. " +
                          "Answer questions based ONLY on the provided context from the game rules and documentation. " +
                          "If the context doesn't contain the answer, say so clearly. " +
                          "Always cite the page number when referencing specific rules.";

        // Debug: emit prompt context event (Issue #4916)
        // Server-side role check: systemPrompt only visible to Admin/Editor roles
        var role = command.UserRole?.ToLowerInvariant();
        var isAdminCaller = role is "admin" or "superadmin" or "editor";
        var estimatedPromptTokens = (systemPrompt.Length + (ragContext.Length > 0 ? ragContext.Length : command.UserQuestion.Length)) / 4;

        var userPrompt = string.IsNullOrEmpty(ragContext)
            ? $"Question: {command.UserQuestion}\n\nNote: No relevant context found in knowledge base."
            : $"Context from game documents:\n{ragContext}\n\nQuestion: {command.UserQuestion}\n\nProvide a clear answer based on the context above.";

        yield return CreateEvent(
            StreamingEventType.DebugPromptContext,
            new
            {
                systemPrompt = isAdminCaller ? systemPrompt : "[redacted]",
                userPromptPreview = userPrompt.Length > 500 ? string.Concat(userPrompt.AsSpan(0, 497), "...") : userPrompt,
                estimatedPromptTokens,
                hasRagContext = !string.IsNullOrEmpty(ragContext),
                contextChunkCount = relevantChunks.Count
            });

        // Budget Display System: Check user budget and fallback to free model if exhausted
        var requestedModel = agentConfig.LlmModel;
        var modelToUse = requestedModel;

        // Estimate query cost (rough: 500 input + 200 output = 700 total tokens)
        var estimatedTokens = (systemPrompt.Length + userPrompt.Length) / 4 + 200;

        // Budget check (fail-open on error)
        var hasBudget = true;
        try
        {
            hasBudget = await _userBudgetService
                .HasBudgetForQueryAsync(command.UserId, estimatedTokens, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fail-open: budget check failed, assume budget available
            _logger.LogWarning(ex,
                "Budget check failed for user {UserId}, assuming budget available",
                command.UserId);
        }

        if (!hasBudget)
        {
            modelToUse = _modelOverrideService.GetModelForBudgetConstraint(requestedModel, budgetExhausted: true);
            _logger.LogWarning(
                "User {UserId} budget exhausted, using fallback model: {Requested} → {Fallback}",
                command.UserId, requestedModel, modelToUse);

            // Send status update about free model usage
            yield return CreateEvent(
                StreamingEventType.StateUpdate,
                new { status = $"Budget low - using free model ({modelToUse})" });
        }

        _logger.LogInformation("Using model: {Model} (requested: {Requested})", modelToUse, requestedModel);

        // === LLM Call with Retry + Tiered Fallback ===
        var originalModel = modelToUse;
        string? fallbackReason = null;

        // Attempt 1: Primary model
        var llmResult = await _llmService.GenerateCompletionWithModelAsync(
            modelToUse, systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogWarning("Primary LLM call failed ({Model}): {Error}. Retrying in 2s...",
                modelToUse, llmResult.ErrorMessage);
            fallbackReason = llmResult.ErrorMessage?.Contains("429", StringComparison.Ordinal) == true
                ? "rate_limited"
                : "provider_unavailable";

            // Attempt 2: Retry same model after 2s delay (heartbeat keeps SSE alive through proxies)
            yield return CreateEvent(StreamingEventType.Heartbeat, new StreamingHeartbeat("retrying"));
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            llmResult = await _llmService.GenerateCompletionWithModelAsync(
                modelToUse, systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!llmResult.Success)
        {
            _logger.LogWarning("Retry failed ({Model}). Selecting fallback model...", modelToUse);

            var fallbackModel = GetFallbackModel(modelToUse);

            if (fallbackModel != null && !string.Equals(fallbackModel, modelToUse, StringComparison.Ordinal))
            {
                _logger.LogInformation("Falling back: {Original} -> {Fallback}", modelToUse, fallbackModel);
                modelToUse = fallbackModel;

                // Attempt 3: Fallback model
                llmResult = await _llmService.GenerateCompletionWithModelAsync(
                    modelToUse, systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("No alternative fallback model available for {Model} (fallback={Fallback})",
                    modelToUse, fallbackModel);
            }
        }

        // All attempts exhausted
        if (!llmResult.Success)
        {
            _logger.LogError("All LLM attempts failed. Original={Original}, Last={Last}, Error={Error}",
                originalModel, modelToUse, llmResult.ErrorMessage);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("Il servizio AI non è al momento disponibile. Riprova tra qualche minuto.", "LLM_FAILED"));
            yield break;
        }

        // Emit downgrade notice if model changed
        if (!string.Equals(modelToUse, originalModel, StringComparison.Ordinal))
        {
            var isLocal = string.Equals(modelToUse, AgentDefaults.OllamaFallbackModel, StringComparison.Ordinal);

            yield return CreateEvent(StreamingEventType.ModelDowngrade,
                new StreamingModelDowngrade(
                    OriginalModel: originalModel,
                    FallbackModel: modelToUse,
                    Reason: fallbackReason ?? "provider_unavailable",
                    IsLocalFallback: isLocal,
                    UpgradeMessage: isLocal
                        ? "Il modello gratuito è temporaneamente non disponibile. Risposta generata con modello locale. Passa a Premium per modelli più affidabili."
                        : null
                ));
        }

        // Emit response (non-streaming for POC)
        var fullResponse = llmResult.Response;
        var tokenCount = llmResult.Usage.TotalTokens;

        // Debug: emit cost/token update (Issue #4916)
        yield return CreateEvent(
            StreamingEventType.DebugCostUpdate,
            new
            {
                model = modelToUse,
                promptTokens = llmResult.Usage.PromptTokens,
                completionTokens = llmResult.Usage.CompletionTokens,
                totalTokens = tokenCount,
                confidence = retrievalConfidence
            });

        yield return CreateEvent(
            StreamingEventType.Token,
            new StreamingToken(fullResponse));

        // Persist assistant response with metadata
        if (!string.IsNullOrEmpty(fullResponse))
        {
            thread.AddAssistantMessageWithMetadata(
                content: fullResponse,
                agentType: agent.Type.Value,
                confidence: (float?)retrievalConfidence,
                tokenCount: tokenCount);

            await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Persisted chat to thread {ThreadId}: user msg + assistant msg ({TokenCount} tokens)",
                thread.Id, tokenCount);
        }

        // Complete event (include ThreadId for frontend)
        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 1,
                promptTokens: 0,
                completionTokens: tokenCount,
                totalTokens: tokenCount,
                confidence: retrievalConfidence,
                chatThreadId: thread.Id));
    }

    /// <summary>
    /// Builds formatted context string from retrieved chunks for LLM prompt.
    /// Pattern from AskAgentQuestionCommandHandler.
    /// </summary>
    private static string BuildContextFromChunks(List<SearchResultItem> chunks)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var contextParts = chunks.Select((chunk, index) =>
            $"[{index + 1}] (Score: {chunk.Score:F2}, Page: {chunk.Page})\n{chunk.Text}");

        return string.Join("\n\n---\n\n", contextParts);
    }

    /// <summary>
    /// Selects a fallback model based on the failed model's tier.
    /// Free tier -> Ollama local (zero cost).
    /// Paid tiers -> another model in the same tier.
    /// </summary>
    private string? GetFallbackModel(string failedModel)
    {
        var modelConfig = _modelConfigService.GetModelById(failedModel);

        // Unknown model -> Ollama fallback (safe default)
        if (modelConfig == null)
            return AgentDefaults.OllamaFallbackModel;

        // Free tier -> always fall back to Ollama (zero cost, no exceptions)
        if (modelConfig.Tier == ModelTier.Free)
            return AgentDefaults.OllamaFallbackModel;

        // Paid tiers -> find another model in the exact same tier (different provider preferred)
        var sameTierModels = _modelConfigService.GetAllModels()
            .Where(m => m.Tier == modelConfig.Tier
                      && !string.Equals(m.Id, failedModel, StringComparison.Ordinal)
                      && !string.Equals(m.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer different provider to avoid same upstream outage
        var differentProvider = sameTierModels
            .FirstOrDefault(m => !string.Equals(m.Provider, modelConfig.Provider, StringComparison.OrdinalIgnoreCase));

        if (differentProvider != null)
            return differentProvider.Id;

        // Same provider, different model
        if (sameTierModels.Count > 0)
            return sameTierModels[0].Id;

        // No same-tier alternative -> fall back to Ollama
        return AgentDefaults.OllamaFallbackModel;
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
