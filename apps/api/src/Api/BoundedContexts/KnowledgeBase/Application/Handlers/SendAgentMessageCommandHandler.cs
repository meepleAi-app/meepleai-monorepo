using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Observability;
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
internal sealed partial class SendAgentMessageCommandHandler : IStreamingQueryHandler<SendAgentMessageCommand, RagStreamingEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IEmbeddingService _embeddingService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUserBudgetService _userBudgetService;
    private readonly ILlmModelOverrideService _modelOverrideService;
    private readonly IModelConfigurationService _modelConfigService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IConversationQueryRewriter _queryRewriter;
    private readonly IConversationSummarizer _conversationSummarizer;
    private readonly IUserAiConsentCheckService _consentCheckService;
    private readonly IGameSessionOrchestratorService _sessionOrchestrator;
    private readonly IHybridCacheService _hybridCache;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<SendAgentMessageCommandHandler> _logger;

    /// <summary>Cache TTL for GameSessionContext.</summary>
    private static readonly TimeSpan SessionContextCacheTtl = TimeSpan.FromMinutes(5);

    public SendAgentMessageCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IEmbeddingService embeddingService,
        MeepleAiDbContext dbContext,
        IUserBudgetService userBudgetService,
        ILlmModelOverrideService modelOverrideService,
        IModelConfigurationService modelConfigService,
        ChatContextDomainService chatContextService,
        IConversationQueryRewriter queryRewriter,
        IConversationSummarizer conversationSummarizer,
        IUserAiConsentCheckService consentCheckService,
        IGameSessionOrchestratorService sessionOrchestrator,
        IHybridCacheService hybridCache,
        IRagAccessService ragAccessService,
        ILogger<SendAgentMessageCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userBudgetService = userBudgetService ?? throw new ArgumentNullException(nameof(userBudgetService));
        _modelOverrideService = modelOverrideService ?? throw new ArgumentNullException(nameof(modelOverrideService));
        _modelConfigService = modelConfigService ?? throw new ArgumentNullException(nameof(modelConfigService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _queryRewriter = queryRewriter ?? throw new ArgumentNullException(nameof(queryRewriter));
        _conversationSummarizer = conversationSummarizer ?? throw new ArgumentNullException(nameof(conversationSummarizer));
        _consentCheckService = consentCheckService ?? throw new ArgumentNullException(nameof(consentCheckService));
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
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
        // Issue #5541: TTFT (Time-To-First-Token) SLO metric
        var ttftStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting agent chat for Agent {AgentId}, User {UserId}, Thread {ChatThreadId}",
            command.AgentId, command.UserId, command.ChatThreadId);

        // Issue #5513: Check AI consent — if user opted out, return search-only error
        var aiAllowed = await _consentCheckService
            .IsAiProcessingAllowedAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!aiAllowed)
        {
            _logger.LogInformation(
                "User {UserId} has not consented to AI processing — blocking LLM generation",
                command.UserId);

            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    "AI features are disabled. Enable AI processing in Settings > AI & Privacy to use this feature.",
                    "AI_CONSENT_REQUIRED"));
            yield break;
        }

        // Issue #5580: Session-aware RAG — load session context when GameSessionId is provided
        GameSessionContextDto? sessionContext = null;
        if (command.GameSessionId.HasValue)
        {
            var cacheKey = $"game-session-context:{command.GameSessionId.Value}";
            sessionContext = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async ct => await _sessionOrchestrator.BuildContextAsync(command.GameSessionId.Value, ct).ConfigureAwait(false),
                tags: new[] { $"session:{command.GameSessionId.Value}" },
                expiration: SessionContextCacheTtl,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Loaded session context for session {SessionId}: DegradationLevel={Level}, AllGameIds=[{GameIds}]",
                command.GameSessionId.Value,
                sessionContext.DegradationLevel,
                string.Join(",", sessionContext.AllGameIds));

            // If NoAI degradation, return informative message without hitting RAG
            if (sessionContext.DegradationLevel == SessionDegradationLevel.NoAI)
            {
                _logger.LogInformation(
                    "Session {SessionId} has NoAI degradation — returning informative message",
                    command.GameSessionId.Value);

                yield return CreateEvent(
                    StreamingEventType.Token,
                    new StreamingToken(SessionContextPromptBuilder.GetNoAiDegradationMessage()));
                yield return CreateEvent(
                    StreamingEventType.Complete,
                    new StreamingComplete(
                        estimatedReadingTimeMinutes: 0,
                        promptTokens: 0,
                        completionTokens: 0,
                        totalTokens: 0,
                        confidence: null,
                        chatThreadId: null,
                        strategyTier: "NoAI",
                        executionId: null));
                yield break;
            }
        }

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

        // RAG access enforcement: check game access based on agent's game
        if (agent.GameId is not null && agent.GameId != Guid.Empty)
        {
            var userRole = Enum.TryParse<UserRole>(command.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                command.UserId, agent.GameId.Value, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError("Accesso RAG non autorizzato", "RAG_ACCESS_DENIED"));
                yield break;
            }
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
                LlmModel = AgentDefaults.DefaultModel,
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
                gameId: agent.GameId,
                agentId: command.AgentId,
                agentType: agent.Type.Value,
                title: command.UserQuestion.Length > 100
                    ? string.Concat(command.UserQuestion.AsSpan(0, 97), "...")
                    : command.UserQuestion);

            await _chatThreadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Issue #5256: Build conversation history BEFORE adding current message
        // to avoid duplicating the current question in the history context.
        var chatHistoryContext = string.Empty;
        if (_chatContextService.ShouldIncludeChatHistory(thread))
        {
            chatHistoryContext = _chatContextService.BuildChatHistoryContext(thread);
            if (!string.IsNullOrEmpty(chatHistoryContext))
            {
                _logger.LogInformation(
                    "Including {MessageCount} previous messages in conversation context for thread {ThreadId}",
                    thread.MessageCount, thread.Id);
            }
        }

        // Persist user message
        thread.AddUserMessage(command.UserQuestion);
        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // State update (include ThreadId for frontend to track)
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Starting chat with {agent.Name}", thread.Id));

        // Resolve typology profile for RAG params and prompt differentiation (#5279)
        var typologyName = ResolveTypologyName(agent);
        var profile = TypologyProfile.FromName(typologyName);

        // Debug: emit typology profile event (#5281)
        yield return CreateEvent(
            StreamingEventType.DebugTypologyProfile,
            new
            {
                typology = profile.Name,
                description = profile.Description,
                topK = profile.TopK,
                minScore = profile.MinScore,
                searchStrategy = profile.SearchStrategy,
                temperature = profile.Temperature,
                maxTokens = profile.MaxTokens,
                promptPreview = profile.SystemPromptTemplate.Length > 200
                    ? string.Concat(profile.SystemPromptTemplate.AsSpan(0, 197), "...")
                    : profile.SystemPromptTemplate
            });

        // RAG Pipeline: Retrieve relevant context from Knowledge Base
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base...", thread.Id));

        // Issue #5258: Rewrite ambiguous follow-up queries for better RAG retrieval
        // Issue #5543: Distributed tracing — child spans for RAG pipeline stages
        var searchQuery = command.UserQuestion;
        if (!string.IsNullOrEmpty(chatHistoryContext))
        {
            using var rewriteActivity = MeepleAiActivitySources.Rag.StartActivity("Rag.QueryRewrite");
            rewriteActivity?.SetTag("rag.original_query_length", command.UserQuestion.Length);
            searchQuery = await _queryRewriter.RewriteQueryAsync(
                command.UserQuestion, chatHistoryContext, cancellationToken).ConfigureAwait(false);
            rewriteActivity?.SetTag("rag.rewritten_query_length", searchQuery.Length);
        }

        // Step 1: Generate embedding for search query (rewritten if applicable)
        // Issue #5543: Distributed tracing — child span for embedding generation
        using var embeddingActivity = MeepleAiActivitySources.Rag.StartActivity("Rag.Embedding");
        embeddingActivity?.SetTag("rag.query_length", searchQuery.Length);
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
            searchQuery,
            cancellationToken).ConfigureAwait(false);
        embeddingActivity?.SetTag("rag.embedding_success", embeddingResult.Success);
        embeddingActivity?.Dispose();

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Embedding generation failed for agent {AgentId}", command.AgentId);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("Failed to generate embedding for query", "EMBEDDING_FAILED"));
            yield break;
        }

        // Step 2: Vector search in Qdrant with document filtering
        // Issue #5580: When session context exists, search across all game IDs
        Guid? gameIdForSearch = thread.GameId ?? agent.GameId;
        var pdfDocumentIds = new List<string>();
        IReadOnlyList<Guid>? sessionGameIds = null;

        if (sessionContext != null && sessionContext.AllGameIds.Count > 0)
        {
            sessionGameIds = sessionContext.AllGameIds;
            // Use primary game ID as the main search target, but also search expansions
            gameIdForSearch = sessionContext.PrimaryGameId ?? gameIdForSearch;

            _logger.LogInformation(
                "Session-aware search: {GameCount} games [{GameIds}]",
                sessionGameIds.Count, string.Join(",", sessionGameIds));
        }

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
                limit = profile.TopK,
                minScore = profile.MinScore,
                typology = profile.Name
            });

        // Vector search (Qdrant dependency removed — returns empty results)
        var searchResult = Api.Services.SearchResult.CreateSuccess(new List<SearchResultItem>());

        // Step 3: Filter results by minimum score, deduplicate, and build context
        // Issue #5254: Duplicate Qdrant points cause identical chunks in results.
        // Deduplicate by (PdfId normalized, ChunkIndex), keeping highest score.
        var minScore = profile.MinScore;
        var relevantChunks = searchResult.Results
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .GroupBy(r => (PdfId: r.PdfId.Replace("-", "", StringComparison.Ordinal), r.ChunkIndex))
            .Select(g => g.First())
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

        // Issue #5260: Structured multi-turn prompt template
        // Issue #5279: Use typology-aware prompt with game name
        var hasHistory = !string.IsNullOrEmpty(chatHistoryContext);
        var gameName = await ResolveGameNameAsync(agent, cancellationToken).ConfigureAwait(false);
        var systemPrompt = _chatContextService.BuildSystemPrompt(
            agent.Name, typologyName, gameName, hasHistory);

        // Issue #5580: Inject session context preamble into system prompt
        if (sessionContext != null)
        {
            systemPrompt = SessionContextPromptBuilder.BuildSessionPreamble(sessionContext) + systemPrompt;
        }
        var userPrompt = _chatContextService.BuildStructuredUserPrompt(
            command.UserQuestion,
            ragContext,
            hasHistory ? chatHistoryContext : null);

        // Debug: emit prompt context event (Issue #4916)
        // Server-side role check: systemPrompt only visible to Admin/Editor roles
        var role = command.UserRole?.ToLowerInvariant();
        var isAdminCaller = role is "admin" or "superadmin" or "editor";
        var estimatedPromptTokens = (systemPrompt.Length + userPrompt.Length) / 4;

        yield return CreateEvent(
            StreamingEventType.DebugPromptContext,
            new
            {
                systemPrompt = isAdminCaller ? systemPrompt : "[redacted]",
                userPromptPreview = userPrompt.Length > 500 ? string.Concat(userPrompt.AsSpan(0, 497), "...") : userPrompt,
                estimatedPromptTokens,
                hasRagContext = !string.IsNullOrEmpty(ragContext),
                contextChunkCount = relevantChunks.Count,
                typology = profile.Name,
                typologyTemperature = profile.Temperature
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
        // Issue #5543: Distributed tracing — child span for LLM call
        using var llmActivity = MeepleAiActivitySources.Rag.StartActivity("Rag.LlmCall");
        llmActivity?.SetTag("rag.model", modelToUse);
        llmActivity?.SetTag("rag.estimated_prompt_tokens", estimatedPromptTokens);

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
            llmActivity?.SetTag("rag.llm_success", false);
            llmActivity?.SetTag("rag.llm_error", llmResult.ErrorMessage ?? "unknown");
            _logger.LogError("All LLM attempts failed. Original={Original}, Last={Last}, Error={Error}",
                originalModel, modelToUse, llmResult.ErrorMessage);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("Il servizio AI non è al momento disponibile. Riprova tra qualche minuto.", "LLM_FAILED"));
            yield break;
        }

        llmActivity?.SetTag("rag.llm_success", true);
        llmActivity?.SetTag("rag.final_model", modelToUse);
        llmActivity?.SetTag("rag.used_fallback", !string.Equals(modelToUse, originalModel, StringComparison.Ordinal));
        llmActivity?.SetTag("rag.total_tokens", llmResult.Usage.TotalTokens);

        // Issue #5541: Record TTFT metric (request start → first LLM response)
        ttftStopwatch.Stop();
        MeepleAiMetrics.RagFirstTokenLatency.Record(ttftStopwatch.ElapsedMilliseconds);

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

        // Debug: emit cost/token update (Issue #4916, #93)
        yield return CreateEvent(
            StreamingEventType.DebugCostUpdate,
            new
            {
                model = modelToUse,
                promptTokens = llmResult.Usage.PromptTokens,
                completionTokens = llmResult.Usage.CompletionTokens,
                totalTokens = tokenCount,
                costUsd = (double)llmResult.Cost.TotalCost,
                confidence = retrievalConfidence,
                typology = profile.Name
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

            // Issue #5259: Progressive summarization — after persisting, check if we need
            // to summarize older messages that have slid out of the verbatim window.
            // This runs inline but is fast (small LLM call) and only triggers occasionally.
            var messagesToSummarize = _chatContextService.GetMessagesToSummarize(thread);
            if (messagesToSummarize.Count > 0)
            {
                try
                {
                    var updatedSummary = await _conversationSummarizer.SummarizeAsync(
                        messagesToSummarize, thread.ConversationSummary, cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(updatedSummary))
                    {
                        thread.UpdateConversationSummary(updatedSummary);
                        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
                        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation(
                            "Updated conversation summary for thread {ThreadId} ({SummaryLength} chars)",
                            thread.Id, updatedSummary.Length);
                    }
                }
                catch (Exception ex)
                {
                    // Never fail the response because of summarization
                    _logger.LogWarning(ex, "Failed to update conversation summary for thread {ThreadId}", thread.Id);
                }
            }
        }

        // Derive strategy tier from the model used (Issue #5481: Response Meta Badge)
        var strategyTier = ResolveStrategyTier(modelToUse);

        // Issue #5486: Include executionId for Editor/Admin deep link to Debug Console
        Guid? executionId = isAdminCaller ? Guid.NewGuid() : null;

        // Complete event (include ThreadId for frontend)
        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 1,
                promptTokens: 0,
                completionTokens: tokenCount,
                totalTokens: tokenCount,
                confidence: retrievalConfidence,
                chatThreadId: thread.Id,
                strategyTier: strategyTier,
                executionId: executionId));
    }

}
