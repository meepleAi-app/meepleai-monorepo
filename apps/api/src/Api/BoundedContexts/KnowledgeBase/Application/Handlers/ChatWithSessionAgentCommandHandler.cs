using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ChatWithSessionAgentCommand.
/// Implements SSE streaming for session-based agent chat with full RAG pipeline:
/// embedding → Qdrant search → prompt assembly → LLM streaming → persistence.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// Issue #5313: Wired to real HybridLlmService for LLM streaming.
/// </summary>
internal sealed class ChatWithSessionAgentCommandHandler : IStreamingQueryHandler<ChatWithSessionAgentCommand, RagStreamingEvent>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRagPromptAssemblyService _ragPromptService;
    private readonly ILlmService _llmService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatWithSessionAgentCommandHandler> _logger;

    // Summary generation thresholds
    private const int SummaryThreshold = 10;
    private const int SummaryTriggerDelta = 5;

    public ChatWithSessionAgentCommandHandler(
        IAgentSessionRepository sessionRepository,
        IAgentTypologyRepository typologyRepository,
        IChatThreadRepository chatThreadRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        IRagPromptAssemblyService ragPromptService,
        ILlmService llmService,
        IServiceScopeFactory scopeFactory,
        ILogger<ChatWithSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _typologyRepository = typologyRepository ?? throw new ArgumentNullException(nameof(typologyRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ragPromptService = ragPromptService ?? throw new ArgumentNullException(nameof(ragPromptService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        ChatWithSessionAgentCommand command,
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
        ChatWithSessionAgentCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting session agent chat for AgentSession {SessionId}, User {UserId}, Thread {ChatThreadId}",
            command.AgentSessionId, command.UserId, command.ChatThreadId);

        // Validate AgentSession exists and is active
        var agentSession = await _sessionRepository
            .GetByIdAsync(command.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"AgentSession {command.AgentSessionId} not found", "SESSION_NOT_FOUND"));
            yield break;
        }

        if (!agentSession.IsActive)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"AgentSession {command.AgentSessionId} is not active", "SESSION_INACTIVE"));
            yield break;
        }

        // Load typology for prompt context
        var typology = await _typologyRepository
            .GetByIdAsync(agentSession.TypologyId, cancellationToken)
            .ConfigureAwait(false);

        if (typology == null)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError($"AgentTypology {agentSession.TypologyId} not found", "TYPOLOGY_NOT_FOUND"));
            yield break;
        }

        // Resolve game title
        var game = await _gameRepository.GetByIdAsync(agentSession.GameId, cancellationToken).ConfigureAwait(false);
        var gameTitle = game?.Title ?? "Unknown Game";

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
            // Auto-create thread for new session conversations
            thread = new ChatThread(
                id: Guid.NewGuid(),
                userId: command.UserId,
                agentType: typology.Name,
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

        // State update (include ThreadId for frontend)
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Retrieving game rules and building context...", thread.Id));

        // Assemble prompt with RAG context + chat history
        var assembled = await _ragPromptService.AssemblePromptAsync(
            typology.Name,
            gameTitle,
            agentSession.CurrentGameState,
            command.UserQuestion,
            agentSession.GameId,
            thread,
            userTier: null,
            cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Prompt assembled: {EstimatedTokens} tokens, {CitationCount} citations",
            assembled.EstimatedTokens, assembled.Citations.Count);

        // Stream LLM response token by token
        var fullResponse = new StringBuilder();
        LlmUsage? finalUsage = null;

        // Note: yield return cannot be in try-catch, so we collect chunks and capture errors
        var chunks = new List<StreamChunk>();
        string? streamError = null;
        try
        {
            await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
                assembled.SystemPrompt,
                assembled.UserPrompt,
                RequestSource.AgentTask,
                cancellationToken).ConfigureAwait(false))
            {
                chunks.Add(chunk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM streaming failed for session {SessionId}", command.AgentSessionId);
            streamError = ex.Message;
        }

        if (streamError != null)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("LLM streaming failed: " + streamError, "LLM_ERROR"));
            yield break;
        }

        // Yield all token events
        foreach (var chunk in chunks)
        {
            if (chunk.Content != null)
            {
                fullResponse.Append(chunk.Content);
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content));
            }

            if (chunk.IsFinal)
            {
                finalUsage = chunk.Usage;
            }
        }

        if (fullResponse.Length == 0)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError("LLM returned empty response", "EMPTY_RESPONSE"));
            yield break;
        }

        var responseText = fullResponse.ToString();
        var totalTokens = finalUsage?.TotalTokens ?? 0;

        // Persist assistant response
        thread.AddAssistantMessageWithMetadata(
            content: responseText,
            agentType: typology.Name,
            tokenCount: totalTokens);

        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted session chat to thread {ThreadId}: user msg + assistant msg ({Tokens} tokens, {Chunks} RAG chunks)",
            thread.Id, totalTokens, assembled.Citations.Count);

        // Fire-and-forget: generate conversation summary if thread is long enough
        var activeMessageCount = thread.Messages.Count(m => !m.IsDeleted && !m.IsInvalidated);
        if (activeMessageCount > SummaryThreshold &&
            thread.LastSummarizedMessageCount < activeMessageCount - SummaryTriggerDelta)
        {
            _ = GenerateConversationSummaryAsync(thread.Id);
        }

        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: Math.Max(1, responseText.Length / 1000),
                promptTokens: finalUsage?.PromptTokens ?? 0,
                completionTokens: finalUsage?.CompletionTokens ?? 0,
                totalTokens: totalTokens,
                confidence: RagPromptAssemblyService.ComputeConfidence(assembled.Citations, responseText),
                chatThreadId: thread.Id));
    }

    private async Task GenerateConversationSummaryAsync(Guid threadId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var summarizer = scope.ServiceProvider.GetRequiredService<IConversationSummarizer>();
            var chatRepo = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var thread = await chatRepo.GetByIdAsync(threadId, CancellationToken.None).ConfigureAwait(false);
            if (thread == null) return;

            var messagesToSummarize = thread.Messages
                .Where(m => !m.IsDeleted && !m.IsInvalidated)
                .OrderBy(m => m.SequenceNumber)
                .Take(thread.Messages.Count(m => !m.IsDeleted && !m.IsInvalidated) - RagPromptAssemblyService.RecentMessageCount)
                .ToList();

            if (messagesToSummarize.Count == 0) return;

            var summary = await summarizer.SummarizeAsync(
                messagesToSummarize,
                thread.ConversationSummary,
                CancellationToken.None).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(summary))
            {
                thread.UpdateConversationSummary(summary);
                await chatRepo.UpdateAsync(thread, CancellationToken.None).ConfigureAwait(false);
                await uow.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

                _logger.LogInformation(
                    "Updated conversation summary for thread {ThreadId} ({SummaryLength} chars)",
                    threadId, summary.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fire-and-forget conversation summary failed for thread {ThreadId}", threadId);
        }
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
