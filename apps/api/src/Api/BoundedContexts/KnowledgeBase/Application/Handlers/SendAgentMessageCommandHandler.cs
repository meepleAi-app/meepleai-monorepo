using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

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
    private readonly ILogger<SendAgentMessageCommandHandler> _logger;

    public SendAgentMessageCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        ILogger<SendAgentMessageCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
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

        // Stream LLM response tokens and accumulate full response
        var responseBuilder = new StringBuilder();
        var systemPrompt = $"You are {agent.Name}, a board game AI assistant. Answer questions about board games.";
        int tokenCount = 0;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
            systemPrompt,
            command.UserQuestion,
            cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                responseBuilder.Append(chunk.Content);
                tokenCount++;

                yield return CreateEvent(
                    StreamingEventType.Token,
                    new StreamingToken(chunk.Content));
            }
        }

        // Persist assistant response with metadata
        var fullResponse = responseBuilder.ToString();
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

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
