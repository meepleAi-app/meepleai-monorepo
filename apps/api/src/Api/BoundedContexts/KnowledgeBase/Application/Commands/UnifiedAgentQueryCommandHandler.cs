using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for the Unified API Gateway query command.
/// Uses AgentRouterService for intent-based routing and streams the response.
/// Issue #4336: Multi-Agent Router integration.
/// Issue #4338: Unified API Gateway - /api/v1/agents/query
/// </summary>
internal sealed class UnifiedAgentQueryCommandHandler
    : IStreamingQueryHandler<UnifiedAgentQueryCommand, RagStreamingEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly AgentRouterService _routerService;
    private readonly IUserAiConsentCheckService _consentCheckService;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<UnifiedAgentQueryCommandHandler> _logger;

    public UnifiedAgentQueryCommandHandler(
        IAgentRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        AgentOrchestrationService orchestrationService,
        AgentRouterService routerService,
        IUserAiConsentCheckService consentCheckService,
        IRagAccessService ragAccessService,
        ILogger<UnifiedAgentQueryCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _routerService = routerService ?? throw new ArgumentNullException(nameof(routerService));
        _consentCheckService = consentCheckService ?? throw new ArgumentNullException(nameof(consentCheckService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        UnifiedAgentQueryCommand command,
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
        UnifiedAgentQueryCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Unified gateway query from User {UserId}: \"{Query}\" (preferred={PreferredAgentId})",
            command.UserId, command.Query, command.PreferredAgentId);

        // Issue #5513: Check AI consent — if user opted out, block LLM generation
        var aiAllowed = await _consentCheckService
            .IsAiProcessingAllowedAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!aiAllowed)
        {
            _logger.LogInformation(
                "User {UserId} has not consented to AI processing — blocking unified query",
                command.UserId);

            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    "AI features are disabled. Enable AI processing in Settings > AI & Privacy to use this feature.",
                    "AI_CONSENT_REQUIRED"));
            yield break;
        }

        // RAG access enforcement
        if (command.GameId.HasValue)
        {
            var userRole = Enum.TryParse<UserRole>(command.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                command.UserId, command.GameId.Value, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        // Phase 1: Agent Selection (via Router or explicit preference)
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Classifying query intent..."));

        Agent? selectedAgent;
        AgentRoutingDecision? routingDecision = null;

        if (command.PreferredAgentId.HasValue)
        {
            // User explicitly requested a specific agent
            selectedAgent = await _agentRepository
                .GetByIdAsync(command.PreferredAgentId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (selectedAgent == null || !selectedAgent.IsActive)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError(
                        $"Preferred agent {command.PreferredAgentId.Value} not found or inactive",
                        "PREFERRED_AGENT_NOT_FOUND"));
                yield break;
            }
        }
        else
        {
            // Intent-based routing via AgentRouterService
            routingDecision = _routerService.RouteQuery(command.Query);

            var allAgents = await _agentRepository
                .GetAllActiveAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allAgents.Count == 0)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError("No active agents available", "NO_AGENTS_AVAILABLE"));
                yield break;
            }

            // Map routing decision to actual agent entity
            selectedAgent = FindAgentByRouterTarget(allAgents, routingDecision.TargetAgent);

            // Fallback: use orchestration service if router target not found
            if (selectedAgent == null)
            {
                _logger.LogWarning(
                    "Router target agent {TargetAgent} not found in active agents, falling back to orchestration service",
                    routingDecision.TargetAgent);

                selectedAgent = _orchestrationService.SelectAgentForQuery(command.Query, allAgents);
            }

            if (selectedAgent == null)
            {
                yield return CreateEvent(
                    StreamingEventType.Error,
                    new StreamingError("Could not determine appropriate agent for this query", "AGENT_SELECTION_FAILED"));
                yield break;
            }
        }

        _logger.LogInformation(
            "Unified gateway routed to agent {AgentId} ({AgentName}, type={AgentType})",
            selectedAgent.Id, selectedAgent.Name, selectedAgent.Type.Value);

        // Phase 2: Notify routing decision (with metadata from router)
        var routingMessage = routingDecision != null
            ? $"Routed to {selectedAgent.Name} ({selectedAgent.Type.Value}) — intent: {routingDecision.Intent}, confidence: {routingDecision.Confidence:F2}"
            : $"Routed to {selectedAgent.Name} ({selectedAgent.Type.Value}) — explicit selection";

        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate(routingMessage));

        // Phase 3: Resolve or create ChatThread
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
                    new StreamingError(
                        $"ChatThread {command.ChatThreadId.Value} not found",
                        "THREAD_NOT_FOUND"));
                yield break;
            }
        }
        else
        {
            thread = new ChatThread(
                id: Guid.NewGuid(),
                userId: command.UserId,
                agentId: selectedAgent.Id,
                agentType: selectedAgent.Type.Value,
                title: command.Query.Length > 100
                    ? string.Concat(command.Query.AsSpan(0, 97), "...")
                    : command.Query);

            await _chatThreadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Persist user message
        thread.AddUserMessage(command.Query);
        await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // State update with ThreadId
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Generating response from {selectedAgent.Name}...", thread.Id));

        // Phase 4: Stream LLM response
        var responseBuilder = new StringBuilder();
        var systemPrompt = BuildSystemPrompt(selectedAgent);
        int tokenCount = 0;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
            systemPrompt,
            command.Query,
            RequestSource.Manual,
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

        // Phase 5: Persist assistant response
        var fullResponse = responseBuilder.ToString();
        if (!string.IsNullOrEmpty(fullResponse))
        {
            thread.AddAssistantMessageWithMetadata(
                content: fullResponse,
                agentType: selectedAgent.Type.Value,
                confidence: (float)(routingDecision?.Confidence ?? 0.85),
                tokenCount: tokenCount);

            await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Unified gateway persisted response to thread {ThreadId}: agent={AgentName}, tokens={TokenCount}",
                thread.Id, selectedAgent.Name, tokenCount);
        }

        // Phase 6: Complete event with routing metadata
        yield return CreateEvent(
            StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: Math.Max(1, tokenCount / 250),
                promptTokens: 0,
                completionTokens: tokenCount,
                totalTokens: tokenCount,
                confidence: routingDecision?.Confidence ?? 0.85,
                chatThreadId: thread.Id,
                routingIntent: routingDecision?.Intent.ToString(),
                routingLatencyMs: routingDecision?.RoutingDuration.TotalMilliseconds));
    }

    /// <summary>
    /// Maps the router's target agent name to an actual Agent entity.
    /// </summary>
    private static Agent? FindAgentByRouterTarget(List<Agent> agents, string targetAgentName)
    {
        // Map router agent names to AgentType values
        var typeValue = targetAgentName switch
        {
            "TutorAgent" => AgentType.RagAgent.Value,          // Tutor uses RAG agent type
            "ArbitroAgent" => AgentType.RulesInterpreter.Value, // Arbitro validates rules
            "DecisoreAgent" => AgentType.RagAgent.Value,        // Decisore uses RAG for analysis
            _ => null
        };

        if (typeValue == null)
            return null;

        return agents.FirstOrDefault(a =>
            string.Equals(a.Type.Value, typeValue, StringComparison.Ordinal))
            ?? agents.FirstOrDefault(a =>
                a.Name.Contains(targetAgentName.Replace("Agent", ""), StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSystemPrompt(Agent agent)
    {
        var basePrompt = $"You are {agent.Name}, a specialized board game AI assistant.";

        return agent.Type.Value switch
        {
            "RulesInterpreter" => $"{basePrompt} You specialize in interpreting and explaining game rules clearly and accurately. Always cite specific rule sections when possible.",
            "CitationAgent" => $"{basePrompt} You specialize in finding and verifying source citations from game manuals and rulebooks. Always provide exact page references.",
            "ConfidenceAgent" => $"{basePrompt} You specialize in assessing the confidence and accuracy of game-related information. Provide clear confidence levels for your answers.",
            "ConversationAgent" => $"{basePrompt} You are a friendly conversational assistant focused on board game discussions. Maintain context from the conversation history.",
            _ => $"{basePrompt} Answer questions about board games using the knowledge base. Provide helpful, accurate responses with citations when available.",
        };
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
