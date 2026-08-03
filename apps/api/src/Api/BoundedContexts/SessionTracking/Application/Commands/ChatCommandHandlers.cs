using MediatR;
using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using Api.Services.ImageProcessing;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for sending a chat message in a session.
/// Issue #4760 - Shared Chat
/// </summary>
public class SendSessionChatMessageCommandHandler : IRequestHandler<SendSessionChatMessageCommand, SendChatMessageResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly IMediator _mediator;

    public SendSessionChatMessageCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        IMediator mediator)
    {
        _sessionRepository = sessionRepository;
        _chatRepository = chatRepository;
        _mediator = mediator;
    }

    public async Task<SendChatMessageResult> Handle(SendSessionChatMessageCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)
            ?? throw new NotFoundException($"Participant {request.SenderId} not found in session");

        var sequenceNumber = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var message = SessionChatMessage.CreateTextMessage(
            request.SessionId,
            request.SenderId,
            request.Content,
            sequenceNumber,
            request.TurnNumber,
            request.MentionsJson);

        await _chatRepository.AddAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = message.Id,
            SenderId = request.SenderId,
            MessageType = SessionChatMessageType.Text,
            Content = request.Content,
            TurnNumber = request.TurnNumber,
        }, cancellationToken).ConfigureAwait(false);

        return new SendChatMessageResult(message.Id, sequenceNumber);
    }
}

/// <summary>
/// Handler for sending system event messages.
/// </summary>
public class SendSystemEventCommandHandler : IRequestHandler<SendSystemEventCommand, SendChatMessageResult>
{
    private readonly ISessionChatRepository _chatRepository;

    public SendSystemEventCommandHandler(ISessionChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<SendChatMessageResult> Handle(SendSystemEventCommand request, CancellationToken cancellationToken)
    {
        var sequenceNumber = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var message = SessionChatMessage.CreateSystemEvent(
            request.SessionId,
            request.Content,
            sequenceNumber,
            request.TurnNumber);

        await _chatRepository.AddAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SendChatMessageResult(message.Id, sequenceNumber);
    }
}

/// <summary>
/// Handler for asking the RAG agent a question in session context.
/// Issue #5313: Wired to real HybridLlmService for LLM completions.
/// Supports optional image attachments for vision-based game state analysis.
/// </summary>
internal class AskSessionAgentCommandHandler : IRequestHandler<AskSessionAgentCommand, AskSessionAgentResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly IMediator _mediator;
    private readonly ILlmService _llmService;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IGameStateExtractor _gameStateExtractor;
    private readonly ILogger<AskSessionAgentCommandHandler> _logger;
    // #3390 Slice 2: optional (null-safe) so existing 7-arg test construction keeps compiling —
    // null feature flags means the flag is treated as OFF (current multimodal-only behavior).
    private readonly IFeatureFlagService? _featureFlags;
    private readonly IOptions<SessionAgentOptions> _sessionAgentOptions;

    public AskSessionAgentCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        IMediator mediator,
        ILlmService llmService,
        IImagePreprocessor imagePreprocessor,
        IGameStateExtractor gameStateExtractor,
        ILogger<AskSessionAgentCommandHandler> logger,
        IFeatureFlagService? featureFlags = null,
        IOptions<SessionAgentOptions>? sessionAgentOptions = null)
    {
        _sessionRepository = sessionRepository;
        _chatRepository = chatRepository;
        _mediator = mediator;
        _llmService = llmService;
        _imagePreprocessor = imagePreprocessor;
        _gameStateExtractor = gameStateExtractor;
        _logger = logger;
        _featureFlags = featureFlags;
        _sessionAgentOptions = sessionAgentOptions ?? Options.Create(new SessionAgentOptions());
    }

    public async Task<AskSessionAgentResult> Handle(AskSessionAgentCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)
            ?? throw new NotFoundException($"Participant {request.SenderId} not found in session");

        // #3390 Slice 3: extract the vision board-state BEFORE writing the user message, so an empty
        // turn text can be replaced by a query derived from the vision (non-blocking, returns cached).
        var gameState = await _gameStateExtractor.ExtractIfNeededAsync(
            request.SessionId, null, cancellationToken).ConfigureAwait(false);

        // #3390 Slice 3: empty turn text + flag ON → derive the retrieval query from the vision
        // board-state. The derived query is BOTH the retrieval query and the user-visible chat-message
        // content (transparent). Deterministic: fires ONLY on truly-empty text, never overriding a
        // real question. Flag rag.live-vision-query-expansion, default OFF.
        var effectiveQuery = request.Question;
        if (string.IsNullOrWhiteSpace(request.Question)
            && _featureFlags is not null
            && await _featureFlags.IsEnabledAsync(FeatureFlagConstants.LiveVisionQueryExpansionKey).ConfigureAwait(false))
        {
            effectiveQuery = DeriveQueryFromVision(gameState);
            _logger.LogInformation(
                "Empty turn text for session {SessionId}; derived retrieval query from vision board-state",
                request.SessionId);
        }

        // Save the user's question (or the vision-derived query) as a chat message.
        var userSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var userMessage = SessionChatMessage.CreateTextMessage(
            request.SessionId,
            request.SenderId,
            effectiveQuery,
            userSeq,
            request.TurnNumber);

        await _chatRepository.AddAsync(userMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Build system prompt with optional game state context
        var agentType = "tutor";
        var systemPrompt = $"You are a helpful board game tutor assisting during a game session (Game ID: {session.GameId}). " +
                           "Answer questions about rules, strategy, and gameplay concisely.";

        if (!string.IsNullOrWhiteSpace(gameState))
        {
            systemPrompt += $"\n\nCurrent game state from board analysis:\n{gameState}";
        }

        // Initialized to the ungrounded defaults; the grounded path (below) overwrites them on success.
        string answer = string.Empty;
        float? confidence = null;
        string? citationsJson = null;
        var groundingStatus = GroundingStatus.Ungrounded;
        var retrievalProfileTag = MeepleAiMetrics.AgentRetrievalProfiles.None;
        var citationCount = 0;
        var hasImages = request.Images is { Count: > 0 };

        // #3390 Slice 2/3: the (possibly vision-derived) query drives retrieval and the LLM prompt.
        var retrievalQuery = effectiveQuery;

        // Feature-gated (rag.live-image-retrieval, default OFF). Requires non-empty text (the
        // empty-text case is Slice 3). Null feature-flag service (test construction) ⇒ treated as OFF.
        var liveImageRetrievalEnabled = _featureFlags is not null
            && !string.IsNullOrWhiteSpace(retrievalQuery)
            && await _featureFlags.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey).ConfigureAwait(false);

        var groundedProduced = false;
        if (liveImageRetrievalEnabled)
        {
            var userId = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)?.UserId ?? Guid.Empty;
            using var retrievalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            retrievalCts.CancelAfter(TimeSpan.FromMilliseconds(_sessionAgentOptions.Value.RetrievalBudgetMs));
            try
            {
                // KnowledgeBase owns "grounded answer on the rulebook" — consumed via IMediator
                // (DDD Customer/Supplier), never by injecting a KB service. Vision stays as state
                // context (gameState); the turn text is the retrieval query.
                var grounded = await _mediator.Send(
                    new AskGroundedSessionQuery(session.GameId, retrievalQuery, gameState, userId),
                    retrievalCts.Token).ConfigureAwait(false);

                if (hasImages && grounded.Citations.Count == 0)
                {
                    // Image attached but rulebook retrieval found nothing → fall through to the
                    // multimodal path so THIS turn's image is actually analyzed. A text-only
                    // ungrounded answer would silently drop the photo — strictly worse than the
                    // pre-flag behavior. Text-only turns keep the (ungrounded) grounded answer,
                    // since there is no image to analyze. groundedProduced stays false.
                    _logger.LogInformation(
                        "Grounded retrieval returned no citations for image turn (session {SessionId}); falling back to multimodal to analyze the image",
                        request.SessionId);
                }
                else
                {
                    answer = grounded.Answer;
                    confidence = grounded.Confidence.HasValue ? (float)grounded.Confidence.Value : null;
                    citationCount = grounded.Citations.Count;
                    citationsJson = citationCount > 0 ? JsonSerializer.Serialize(grounded.Citations) : null;
                    groundingStatus = grounded.GroundingStatus;
                    retrievalProfileTag = MeepleAiMetrics.AgentRetrievalProfiles.LiveSession;
                    groundedProduced = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client aborted the whole request (not the retrieval budget) → propagate, do NOT
                // fail-open (which would waste a second LLM call and pollute the fallback metric).
                throw;
            }
            catch (OperationCanceledException oce) when (retrievalCts.IsCancellationRequested)
            {
                // Budget expired → degrade to multimodal-only (fail-open). The response must not wait.
                _logger.LogWarning(oce,
                    "Grounded retrieval exceeded the {BudgetMs}ms budget for session {SessionId}; degrading to multimodal (ungrounded)",
                    _sessionAgentOptions.Value.RetrievalBudgetMs, request.SessionId);
                MeepleAiMetrics.RecordRetrievalFallback(
                    MeepleAiMetrics.RagFallbackTypes.RetrievalBudget, MeepleAiMetrics.RagFallbackSeverity.PartialLoss);
            }
            catch (Exception ex)
            {
                // Any retrieval/generation failure → fail-open to the existing multimodal path.
                _logger.LogWarning(ex,
                    "Grounded retrieval failed for session {SessionId}; degrading to multimodal (ungrounded)",
                    request.SessionId);
                MeepleAiMetrics.RecordRetrievalFallback(
                    MeepleAiMetrics.RagFallbackTypes.Unknown, MeepleAiMetrics.RagFallbackSeverity.PartialLoss);
            }
        }

        if (!groundedProduced)
        {
        try
        {
            if (hasImages)
            {
                // Vision path: process images and build multimodal messages
                var contentParts = new List<ContentPart>();

                foreach (var img in request.Images!)
                {
                    var processed = await _imagePreprocessor.ProcessAsync(
                        img.Data, img.MediaType).ConfigureAwait(false);
                    var base64 = Convert.ToBase64String(processed.Data);
                    contentParts.Add(new ImageContentPart(base64, processed.MediaType));
                }

                contentParts.Add(new TextContentPart(effectiveQuery));

                var messages = new List<LlmMessage>
                {
                    LlmMessage.FromText("system", systemPrompt),
                    new("user", contentParts)
                };

                var result = await _llmService.GenerateMultimodalCompletionAsync(
                    (IReadOnlyList<LlmMessage>)messages,
                    RequestSource.AgentTask,
                    cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    answer = result.Response;
                    confidence = null; // #3388: no fabricated confidence — vision path is not grounded in retrieval
                }
                else
                {
                    _logger.LogWarning(
                        "Multimodal LLM completion failed for session {SessionId}: {Error}",
                        request.SessionId, result.ErrorMessage);
                    answer = "I'm sorry, I couldn't analyze the image right now. Please try again.";
                    confidence = null;
                }
            }
            else
            {
                // Text-only path (existing behavior)
                var result = await _llmService.GenerateCompletionAsync(
                    systemPrompt,
                    effectiveQuery,
                    RequestSource.AgentTask,
                    cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    answer = result.Response;
                    confidence = null; // #3388: no fabricated confidence — text-only path is not grounded in retrieval
                }
                else
                {
                    _logger.LogWarning(
                        "LLM completion failed for session {SessionId}: {Error}",
                        request.SessionId, result.ErrorMessage);
                    answer = "I'm sorry, I couldn't process your question right now. Please try again.";
                    confidence = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM service error for session {SessionId}", request.SessionId);
            answer = "I'm sorry, an error occurred while processing your question. Please try again.";
            confidence = null;
        }
        } // end if (!groundedProduced) — multimodal/text-only fallback (ungrounded)

        var agentSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var agentMessage = SessionChatMessage.CreateAgentResponse(
            request.SessionId,
            answer,
            agentSeq,
            agentType,
            confidence,
            citationsJson,
            request.TurnNumber);

        await _chatRepository.AddAsync(agentMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = agentMessage.Id,
            SenderId = null,
            MessageType = SessionChatMessageType.AgentResponse,
            Content = answer,
            TurnNumber = request.TurnNumber,
        }, cancellationToken).ConfigureAwait(false);

        // #3390 Slice 1/2: structured grounding observability. retrieval_profile=live_session when the
        // grounded path produced the answer (Slice 2), none when it fell back to multimodal/text-only.
        // groundingStatus is Grounded iff the grounded path returned citations. Guarded so a metrics
        // fault can never break the agent response.
        try
        {
            MeepleAiMetrics.RecordAgentResponseGrounding(
                path: hasImages ? MeepleAiMetrics.AgentResponsePaths.Image : MeepleAiMetrics.AgentResponsePaths.Text,
                groundingStatusWire: groundingStatus.ToString(),
                retrievalProfile: retrievalProfileTag,
                citationCount: citationCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "agent-response grounding metric emission failed for session {SessionId}", request.SessionId);
        }

        return new AskSessionAgentResult(agentMessage.Id, answer, agentType, agentMessage.Confidence, citationsJson, groundingStatus);
    }

    // #3390 Slice 3: default retrieval query when the turn has no text and no usable vision state.
    private const string VisionQueryFallback = "What should I do now based on the current board state?";

    /// <summary>
    /// Derives a concise retrieval query from the GameStateExtractor vision JSON (observations +
    /// game_phase + scalar visible_elements keywords). Defensive: any parse failure or empty state
    /// falls back to <see cref="VisionQueryFallback"/> so the turn never breaks. The result is used
    /// both as the retrieval query and as the user-visible chat message (transparency).
    /// </summary>
    private static string DeriveQueryFromVision(string? gameState)
    {
        if (string.IsNullOrWhiteSpace(gameState))
        {
            return VisionQueryFallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(gameState);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return VisionQueryFallback;
            }

            var parts = new List<string>();

            if (root.TryGetProperty("observations", out var obs) && obs.ValueKind == JsonValueKind.String)
            {
                var o = obs.GetString();
                if (!string.IsNullOrWhiteSpace(o)) parts.Add(o!.Trim());
            }

            if (root.TryGetProperty("game_phase", out var phase) && phase.ValueKind == JsonValueKind.String)
            {
                var p = phase.GetString();
                if (!string.IsNullOrWhiteSpace(p)) parts.Add($"phase: {p!.Trim()}");
            }

            if (root.TryGetProperty("visible_elements", out var ve))
            {
                CollectScalarText(ve, parts);
            }

            if (parts.Count == 0)
            {
                return VisionQueryFallback;
            }

            var summary = string.Join("; ", parts);
            if (summary.Length > 400)
            {
                summary = summary[..400];
            }

            return $"Given the current board state ({summary}), what can I do on my turn?";
        }
        catch (JsonException)
        {
            return VisionQueryFallback;
        }
    }

    /// <summary>
    /// Recursively collects non-empty string leaves from a JSON element (visible_elements can nest
    /// objects/arrays). Numbers/bools/null are skipped — they are not useful retrieval terms.
    /// </summary>
    private static void CollectScalarText(JsonElement element, List<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var s = element.GetString();
                if (!string.IsNullOrWhiteSpace(s)) sink.Add(s!.Trim());
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectScalarText(item, sink);
                }
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    CollectScalarText(prop.Value, sink);
                }
                break;
        }
    }
}

/// <summary>
/// Handler for deleting a chat message.
/// </summary>
public class DeleteChatMessageCommandHandler : IRequestHandler<DeleteChatMessageCommand, Unit>
{
    private readonly ISessionChatRepository _chatRepository;
    private readonly ISessionRepository _sessionRepository;

    public DeleteChatMessageCommandHandler(
        ISessionChatRepository chatRepository,
        ISessionRepository sessionRepository)
    {
        _chatRepository = chatRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<Unit> Handle(DeleteChatMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _chatRepository.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Message {request.MessageId} not found");

        if (message.MessageType != SessionChatMessageType.Text)
            throw new ConflictException("Only text messages can be deleted.");

        // #2655 IDOR guard: resolve the sending participant server-side from the
        // authenticated caller — never trust a client-supplied requester id. Only
        // the user who owns the sending participant may delete the message.
        var session = await _sessionRepository.GetByIdAsync(message.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {message.SessionId} not found");
        var sender = message.SenderId.HasValue
            ? session.Participants.FirstOrDefault(p => p.Id == message.SenderId.Value)
            : null;
        if (sender is null || sender.UserId != request.RequesterUserId)
            throw new ForbiddenException("Only the message sender can delete it.");

        message.SoftDelete();
        await _chatRepository.UpdateAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
