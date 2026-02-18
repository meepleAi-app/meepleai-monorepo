using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SendAgentMessageCommandHandler> _logger;

    public SendAgentMessageCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        MeepleAiDbContext dbContext,
        ILogger<SendAgentMessageCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    "Agent non configurato. Configura l'agente prima di chattare.",
                    "AGENT_NOT_CONFIGURED"));
            yield break;
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

            pdfDocumentIds = vectorDocs.Select(vd => vd.PdfDocumentId.ToString()).ToList();
        }

        var gameId = gameIdForSearch?.ToString() ?? "default";

        _logger.LogInformation(
            "Searching for game {GameId} with {DocumentCount} PDF document filters",
            gameId, pdfDocumentIds.Count);

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

        _logger.LogInformation(
            "Retrieved {ChunkCount} relevant chunks (score >= {MinScore}) for agent {AgentId}",
            relevantChunks.Count, minScore, command.AgentId);

        var ragContext = BuildContextFromChunks(relevantChunks);

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

        var userPrompt = string.IsNullOrEmpty(ragContext)
            ? $"Question: {command.UserQuestion}\n\nNote: No relevant context found in knowledge base."
            : $"Context from game documents:\n{ragContext}\n\nQuestion: {command.UserQuestion}\n\nProvide a clear answer based on the context above.";

        // POC: Use explicit model from config (Haiku) instead of routing (DeepSeek unavailable)
        _logger.LogInformation("POC using configured model: {Model}", agentConfig.LlmModel);

        var llmResult = await _llmService.GenerateCompletionWithModelAsync(
            agentConfig.LlmModel,
            systemPrompt,
            userPrompt,
            cancellationToken).ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogError("LLM failed: {Error}", llmResult.ErrorMessage);
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"LLM failed: {llmResult.ErrorMessage}", "LLM_FAILED"));
            yield break;
        }

        // Emit response (non-streaming for POC)
        var fullResponse = llmResult.Response;
        var tokenCount = llmResult.Usage.TotalTokens;

        yield return CreateEvent(
            StreamingEventType.Token,
            new StreamingToken(fullResponse));

        // Persist assistant response with metadata
        if (!string.IsNullOrEmpty(fullResponse))
        {
            thread.AddAssistantMessageWithMetadata(
                content: fullResponse,
                agentType: agent.Type.Value,
                confidence: 0.85f,
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
                confidence: 0.85,
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

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
