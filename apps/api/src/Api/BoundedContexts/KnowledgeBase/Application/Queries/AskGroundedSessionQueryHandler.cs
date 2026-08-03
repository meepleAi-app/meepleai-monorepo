using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Thrown when the grounded generation step fails (LLM unavailable/error). SessionTracking
/// catches this to degrade to its multimodal-only path (which stays Ungrounded, #3388).
/// </summary>
public sealed class GroundedSessionGenerationException : Exception
{
    public GroundedSessionGenerationException(string? message) : base(message) { }
}

/// <summary>
/// #3390 Slice 2 — produces a grounded, non-streaming answer for the in-session agent.
///
/// Owns the retrieval + generation shape (assemble RAG prompt under <c>RetrievalPolicy.LiveSession</c>
/// → resolve copyright tiers → generate text-only); delegates the shared finalize (copyright leak
/// guard → paraphrase → citation mapping → grounding → confidence) to <see cref="IGroundedAnswerService"/>
/// (#3490 / ADR-090), the same seam the SSE text path uses. The vision board-state
/// (<c>GameStateContext</c>) is injected as prompt context; pixels are NOT re-sent (they were already
/// distilled by GameStateExtractor).
/// </summary>
internal sealed class AskGroundedSessionQueryHandler
    : IQueryHandler<AskGroundedSessionQuery, GroundedSessionAnswerDto>
{
    private readonly IRagPromptAssemblyService _ragPromptService;
    private readonly ICopyrightTierResolver _copyrightTierResolver;
    private readonly ILlmService _llmService;
    private readonly IGroundedAnswerService _groundedAnswerService;

    public AskGroundedSessionQueryHandler(
        IRagPromptAssemblyService ragPromptService,
        ICopyrightTierResolver copyrightTierResolver,
        ILlmService llmService,
        IGroundedAnswerService groundedAnswerService)
    {
        _ragPromptService = ragPromptService ?? throw new ArgumentNullException(nameof(ragPromptService));
        _copyrightTierResolver = copyrightTierResolver ?? throw new ArgumentNullException(nameof(copyrightTierResolver));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _groundedAnswerService = groundedAnswerService ?? throw new ArgumentNullException(nameof(groundedAnswerService));
    }

    public async Task<GroundedSessionAnswerDto> Handle(AskGroundedSessionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Assemble RAG prompt with retrieval. LiveSession policy = enhancements OFF, CRAG web
        //    off (#3389/#3390). The turn text is the retrieval query. Honors the caller's
        //    CancellationToken (SessionTracking arms a latency budget on it).
        var assembled = await _ragPromptService.AssemblePromptAsync(
            agentTypology: "tutor",
            gameTitle: string.Empty,
            gameState: null,
            userQuestion: request.Question,
            gameId: request.GameId,
            chatThread: null,
            userTier: null,
            agentLanguage: request.AgentLanguage,
            cancellationToken,
            retrievalPolicy: RetrievalPolicy.LiveSession).ConfigureAwait(false);

        // 2. Resolve copyright tiers for the retrieved citations.
        var resolvedCitations = await _copyrightTierResolver
            .ResolveAsync(assembled.Citations, request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // 3. Inject the vision board-state as CONTEXT (not a rulebook bypass). Text-only from here.
        var systemPrompt = assembled.SystemPrompt;
        if (!string.IsNullOrWhiteSpace(request.GameStateContext))
        {
            systemPrompt += $"\n\nCurrent game state from board analysis:\n{request.GameStateContext}";
        }

        // 4. Generate the answer (text-only; pixels already distilled into GameStateContext).
        var completion = await _llmService
            .GenerateCompletionAsync(systemPrompt, assembled.UserPrompt, RequestSource.AgentTask, cancellationToken)
            .ConfigureAwait(false);

        if (!completion.Success)
        {
            // Let SessionTracking degrade to the multimodal-only path (stays Ungrounded, #3388).
            throw new GroundedSessionGenerationException(completion.ErrorMessage);
        }

        var responseText = completion.Response;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            // Success but empty body — treat as failure so the caller degrades (matches the SSE
            // text path's EMPTY_RESPONSE guard) instead of shipping a blank "grounded" answer,
            // which would also throw at SessionChatMessage.CreateAgentResponse (empty content → 500).
            throw new GroundedSessionGenerationException("empty grounded answer");
        }

        // 5. Shared finalize (#3490 / ADR-090): copyright leak guard (fail-open scan / fail-closed
        //    sanitize) → paraphrase → citation mapping → grounding → confidence. Same seam as the SSE
        //    text path, so the trust-critical logic lives in one place. Honors the caller's token
        //    (a budget/client cancellation during the scan propagates so SessionTracking degrades).
        var grounded = await _groundedAnswerService
            .FinalizeAsync(responseText, resolvedCitations, request.Question, request.AgentLanguage, cancellationToken)
            .ConfigureAwait(false);

        return new GroundedSessionAnswerDto(
            grounded.ResponseText, grounded.CitationDtos, grounded.GroundingStatus, grounded.Confidence);
    }
}
