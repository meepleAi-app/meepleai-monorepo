using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ChatWithAgentCommand.
/// Implements SSE streaming for standalone agent chat (non-session).
/// Issue #4126: API Integration for Agent Chat
/// </summary>
internal sealed class ChatWithAgentCommandHandler : IStreamingQueryHandler<ChatWithAgentCommand, RagStreamingEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILlmService _llmService;
    private readonly ILogger<ChatWithAgentCommandHandler> _logger;

    public ChatWithAgentCommandHandler(
        IAgentRepository agentRepository,
        ILlmService llmService,
        ILogger<ChatWithAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        ChatWithAgentCommand command,
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
        ChatWithAgentCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting agent chat for Agent {AgentId}",
            command.AgentId);

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

        // State update
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Starting chat with {agent.Name}"));

        // Stream LLM response tokens
        var systemPrompt = $"You are {agent.Name}, a board game AI assistant. Answer questions about board games.";
        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
            systemPrompt,
            command.UserQuestion,
            cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return CreateEvent(
                    StreamingEventType.Token,
                    new StreamingToken(chunk.Content));
            }
        }

        // Complete event
        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 1,
                promptTokens: 0,
                completionTokens: 0,
                totalTokens: 0,
                confidence: 0.85));
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
