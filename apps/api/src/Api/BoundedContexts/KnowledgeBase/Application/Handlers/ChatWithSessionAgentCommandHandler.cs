using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ChatWithSessionAgentCommand.
/// Implements SSE streaming for session-based agent chat with game state context.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// Note: Minimal LLM response for AGT-010. Full LLM streaming deferred to AGT-011.
/// ChatThread persistence is active for all messages.
/// </summary>
internal sealed class ChatWithSessionAgentCommandHandler : IStreamingQueryHandler<ChatWithSessionAgentCommand, RagStreamingEvent>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentPromptBuilder _promptBuilder;
    private readonly ILogger<ChatWithSessionAgentCommandHandler> _logger;

    public ChatWithSessionAgentCommandHandler(
        IAgentSessionRepository sessionRepository,
        IAgentTypologyRepository typologyRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        IAgentPromptBuilder promptBuilder,
        ILogger<ChatWithSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _typologyRepository = typologyRepository ?? throw new ArgumentNullException(nameof(typologyRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
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
            new StreamingStateUpdate("Building prompt with game state context", thread.Id));

        // Build enhanced prompt (uses AgentPromptBuilder with game state)
        var enhancedPrompt = _promptBuilder.BuildSessionPrompt(
            typology.Name,
            "Unknown Game",  // Note: Requires Game repository integration in AGT-011
            agentSession.CurrentGameState,
            command.UserQuestion);

        _logger.LogDebug("Enhanced prompt built: {Prompt}", enhancedPrompt);

        // Minimal response (full LLM streaming deferred to AGT-011)
        const string placeholderResponse = "Session agent ready. Full LLM streaming coming in AGT-011.";

        yield return CreateEvent(
            StreamingEventType.Token,
            new StreamingToken(placeholderResponse));

        // Persist assistant response
        thread.AddAssistantMessageWithMetadata(
            content: placeholderResponse,
            agentType: typology.Name,
            tokenCount: 1);

        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted session chat to thread {ThreadId}: user msg + assistant msg",
            thread.Id);

        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 1,
                promptTokens: 0,
                completionTokens: 0,
                totalTokens: 0,
                confidence: null,
                chatThreadId: thread.Id));
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
