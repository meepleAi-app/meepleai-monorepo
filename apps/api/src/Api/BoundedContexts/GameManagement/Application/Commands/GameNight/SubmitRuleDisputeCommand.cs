using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CitationDto = Api.Models.CitationDto;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Command for submitting a rule dispute during a live game session.
/// The handler asks for a verdict that starts from the game's rulebook (KnowledgeBase's grounded
/// path, via <see cref="IMediator"/>), parses it, stores the <see cref="RuleDisputeEntry"/> on the
/// session, and broadcasts a <see cref="DisputeResolvedEvent"/>.
/// Game Night Improvvisata — E3: Arbitro mode.
/// </summary>
/// <remarks>
/// <c>CallerRole</c> is the caller's role, as the session reports it. It matters to the RAG ownership gate
/// (ADR-096), which lets administrators through. Null — an unknown role, or a request without a session —
/// counts as a plain user: when in doubt the gate checks, it does not wave through.
/// </remarks>
public sealed record SubmitRuleDisputeCommand(
    Guid SessionId,
    Guid CallerUserId,
    string Description,
    string RaisedByPlayerName,
    string? CallerRole = null) : IRequest<RuleDisputeResponse>;

/// <summary>
/// Response returned after the AI arbitro resolves a rule dispute.
/// </summary>
public sealed record RuleDisputeResponse(
    Guid Id,
    string Verdict,
    List<string> RuleReferences,
    string? Note);

/// <summary>
/// Validates <see cref="SubmitRuleDisputeCommand"/> inputs.
/// </summary>
public sealed class SubmitRuleDisputeCommandValidator : AbstractValidator<SubmitRuleDisputeCommand>
{
    public SubmitRuleDisputeCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.CallerUserId)
            .NotEmpty()
            .WithMessage("Caller user ID is required");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Dispute description is required")
            .MaximumLength(1000)
            .WithMessage("Dispute description cannot exceed 1000 characters");

        RuleFor(x => x.RaisedByPlayerName)
            .NotEmpty()
            .WithMessage("Player name is required")
            .MaximumLength(100)
            .WithMessage("Player name cannot exceed 100 characters");
    }
}

/// <summary>
/// Handles <see cref="SubmitRuleDisputeCommand"/>.
/// Orchestrates the Arbitro AI pipeline:
///   1. Load LiveGameSession from the in-memory repository.
///   2. Verify session is InProgress.
///   3. Ask for a verdict. With a catalog game and a caller who may query its knowledge base (ADR-096), through
///      KnowledgeBase's grounded path (<see cref="AskGroundedSessionQuery"/>, ADR-090) under a time budget:
///      retrieval on the game's rulebook, copyright tiers, leak guard. In every other case the model is asked
///      directly, with nothing of the rulebook in the prompt, and the verdict says why it does not rest on it.
///   4. Parse the COPERTURA / VERDETTO / NOTA sections of the answer.
///   5. Persist <see cref="RuleDisputeEntry"/> to the session and DB.
///   6. Publish <see cref="DisputeResolvedEvent"/> for SignalR broadcast.
/// </summary>
/// <remarks>
/// #4751: the verdict is saved and sent to the whole table, so it is written at the most restrictive
/// copyright tier (<c>UserId = Guid.Empty</c>: verbatim text only for a free licence or an unprotected
/// category), never at the tier of whoever asked. That tier protects the answer; the retrieval is protected by
/// the ownership gate, checked for whoever raised the dispute. The rule references are the passages that were
/// retrieved, not text the model wrote, and only when the model declares they cover the case.
/// </remarks>
internal sealed class SubmitRuleDisputeCommandHandler
    : IRequestHandler<SubmitRuleDisputeCommand, RuleDisputeResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal const string NoVerdictErrorCode = "arbitro_nessun_verdetto";
    private const string NoVerdictMessage = "L'Arbitro non è riuscito a emettere un verdetto. Riprova tra poco.";

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILlmService _llmService;
    private readonly IMediator _mediator;
    private readonly IAnswerPreconditions _answerPreconditions;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IPublisher _publisher;
    private readonly IOptions<SessionAgentOptions> _sessionAgentOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitRuleDisputeCommandHandler> _logger;

    public SubmitRuleDisputeCommandHandler(
        ILiveSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILlmService llmService,
        IMediator mediator,
        IAnswerPreconditions answerPreconditions,
        ITierEnforcementService tierEnforcementService,
        IPublisher publisher,
        IOptions<SessionAgentOptions> sessionAgentOptions,
        TimeProvider timeProvider,
        ILogger<SubmitRuleDisputeCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _answerPreconditions = answerPreconditions ?? throw new ArgumentNullException(nameof(answerPreconditions));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _sessionAgentOptions = sessionAgentOptions ?? throw new ArgumentNullException(nameof(sessionAgentOptions));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleDisputeResponse> Handle(
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load LiveGameSession — throws NotFoundException if missing
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        // 2. Authorization: caller must be the host or an active participant
        var isHost = session.CreatedByUserId == request.CallerUserId;
        var isParticipant = session.Players.Any(p => p.UserId == request.CallerUserId && p.IsActive);
        if (!isHost && !isParticipant)
        {
            throw new ForbiddenException(
                "You must be a participant or host of this session to submit a rule dispute.");
        }

        // 3. Verify session is InProgress
        if (session.Status != LiveSessionStatus.InProgress)
        {
            throw new ConflictException(
                $"Cannot submit a rule dispute for session in {session.Status} status. " +
                "Session must be InProgress.");
        }

        _logger.LogInformation(
            "SubmitRuleDisputeCommand: SessionId={SessionId}, Player={Player}, Description={Description}",
            request.SessionId, request.RaisedByPlayerName, request.Description);

        // 4. Check session agent query quota before calling LLM
        var canQuery = await _tierEnforcementService
            .CanPerformAsync(request.CallerUserId, TierAction.SessionAgentQuery, cancellationToken)
            .ConfigureAwait(false);

        if (!canQuery)
        {
            var usage = await _tierEnforcementService
                .GetUsageAsync(request.CallerUserId, cancellationToken)
                .ConfigureAwait(false);

            throw new ConflictException(
                $"Hai raggiunto il limite di domande alla sessione ({usage.SessionQueries}/{usage.SessionQueriesMax}). Riprova domani.");
        }

        // 5–6. Ask for the verdict: from the game's rulebook when there is one and the caller may read it,
        // else from the model alone
        var answer = await AskForVerdictAsync(session, request, cancellationToken).ConfigureAwait(false);

        // #4751: a model that answers with nothing has not given a verdict. It used to be parsed into
        // «Nessun verdetto disponibile.», saved as the verdict, broadcast and counted against the quota.
        // An empty answer is a failure like !Success: nothing is saved, charged or published, and the
        // caller gets a readable 503 (the model's error text goes to the log, never to the response).
        var text = ArbiterVerdictFormat.StripCopyrightMarkers(answer.Text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw NoVerdict(request.SessionId, "the model answered with no text", answer.FinishReason, answer.CompletionTokens);
        }

        _logger.LogDebug(
            "Arbitro response for SessionId={SessionId}: {Response}",
            request.SessionId, text);

        // 7. Parse the structured response
        var parsed = ArbiterVerdictFormat.Parse(text);

        // The bare skeleton («VERDETTO:\nNOTA:», or an answer cut right after a header) parses to
        // an empty verdict: same case as an empty answer, not a verdict to save.
        if (string.IsNullOrWhiteSpace(parsed.Verdict))
        {
            throw NoVerdict(request.SessionId, "the answer holds no verdict", answer.FinishReason, answer.CompletionTokens);
        }

        // #4751: the references are the passages that were retrieved — never text the model wrote, which
        // is a quote it cannot have — and only if the model declared that they cover the case. A verdict
        // that does not rest on the rulebook says so, in the verdict itself, and cites nothing. A missing
        // COPERTURA line counts as «no»: fail-honest.
        var basis = answer.Basis == VerdictBasis.Rulebook && !parsed.RulebookCoversTheCase
            ? VerdictBasis.NotCovered
            : answer.Basis;
        var notice = ArbiterVerdictFormat.NoticeFor(basis);
        var verdict = notice is null ? parsed.Verdict : $"{notice} {parsed.Verdict}";
        var ruleReferences = notice is null
            ? ArbiterVerdictFormat.BuildReferences(answer.Citations)
            : new List<string>();

        // 8. Create and attach the RuleDisputeEntry
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: request.Description,
            verdict: verdict,
            ruleReferences: ruleReferences,
            raisedByPlayerName: request.RaisedByPlayerName,
            timestamp: now);

        session.AddDispute(entry);

        // 9. Persist disputes JSONB to DB for restart resilience
        await PersistDisputesToDbAsync(session, cancellationToken).ConfigureAwait(false);

        // 10. Update the in-memory repository
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // 11. Record tier usage after successful verdict
        await _tierEnforcementService
            .RecordUsageAsync(request.CallerUserId, TierAction.SessionAgentQuery, cancellationToken)
            .ConfigureAwait(false);

        // 12. Publish DisputeResolvedEvent for SignalR broadcast
        await _publisher
            .Publish(new DisputeResolvedEvent(session.Id, entry), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Rule dispute resolved: SessionId={SessionId}, DisputeId={DisputeId}, Basis={Basis}, Verdict={Verdict}",
            session.Id, entry.Id, basis, verdict);

        return new RuleDisputeResponse(
            Id: entry.Id,
            Verdict: entry.Verdict,
            RuleReferences: entry.RuleReferences,
            Note: parsed.Note);
    }

    // ── Asking for the verdict ─────────────────────────────────────────────────

    /// <summary>What came back from the model, and what the verdict rests on.</summary>
    /// <param name="Text">The raw answer, markers and all.</param>
    /// <param name="Citations">The passages retrieved, after KnowledgeBase's tier-nulling; empty on the direct path.</param>
    /// <param name="Basis">
    /// <see cref="VerdictBasis.Rulebook"/> only when passages were retrieved and the answer is not the refusal; the
    /// model's COPERTURA line can still downgrade it.
    /// </param>
    /// <param name="FinishReason">Only the direct path reports it; for the log.</param>
    /// <param name="CompletionTokens">Only the direct path reports it; for the log.</param>
    private sealed record VerdictAnswer(
        string Text,
        IReadOnlyList<CitationDto> Citations,
        VerdictBasis Basis,
        string? FinishReason = null,
        int? CompletionTokens = null);

    private async Task<VerdictAnswer> AskForVerdictAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
    {
        // No catalog game (an improvvisata session): there was never a rulebook to look in.
        if (session.GameId is not { } gameId)
        {
            return await AskTheModelAloneAsync(session, request, VerdictBasis.NoGame, cancellationToken)
                .ConfigureAwait(false);
        }

        // ADR-096: the ownership gate protects the RETRIEVAL, and the copyright tier below protects only the
        // ANSWER — a tier does nothing for a manual that already went into the prompt. The check is made for
        // whoever raised the dispute, at the single point every answering path uses (IAnswerPreconditions),
        // not as a copy of it. A private game of someone else is a denial like any other. A denial is not a
        // 403 here: the verdict is given without the rulebook and says why.
        try
        {
            await _answerPreconditions
                .EnsureCanAskAboutGameAsync(request.CallerUserId, request.CallerRole, gameId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RagOwnershipRequiredException denial)
        {
            _logger.LogInformation(
                denial,
                "Rule dispute of SessionId={SessionId}: the RAG ownership gate denied GameId={GameId} to UserId={UserId}; " +
                "the verdict is given without the rulebook",
                request.SessionId, gameId, request.CallerUserId);

            return await AskTheModelAloneAsync(session, request, VerdictBasis.OwnershipNotDeclared, cancellationToken)
                .ConfigureAwait(false);
        }

        return await AskTheRulebookAsync(session, request, gameId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The caller may read the rulebook of the game: KnowledgeBase retrieves from it and finalizes the answer
    /// (copyright tiers, leak guard, citation mapping — ADR-090), under a time budget. <c>UserId = Guid.Empty</c>
    /// is the most restrictive tier: the verdict is saved and sent to every player at the table, so it must not be
    /// written at the level of whoever asked (who may have uploaded the file and own the game).
    /// </summary>
    private async Task<VerdictAnswer> AskTheRulebookAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        // The embedding client alone defaults to a 120 s timeout with retries: without a bound a verdict can
        // wait minutes. On expiry the verdict goes the no-rulebook way and says the rulebook could not be consulted.
        var budgetMs = _sessionAgentOptions.Value.ArbiterBudgetMs;
        using var budget = budgetMs > 0
            ? new CancellationTokenSource(TimeSpan.FromMilliseconds(budgetMs), _timeProvider)
            : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);

        try
        {
            // The dispute alone is the question, and so the retrieval query; the persona and the format
            // are instructions for generation (they would be embedded and matched against the rulebook).
            var grounded = await _mediator
                .Send(
                    new AskGroundedSessionQuery(
                        GameId: gameId,
                        Question: request.Description,
                        GameStateContext: null,
                        UserId: Guid.Empty,
                        AgentLanguage: "it",
                        ResponseInstructions: ArbiterVerdictFormat.ResponseInstructions(session)),
                    linked.Token)
                .ConfigureAwait(false);

            // The leak guard found a verbatim run of a Protected passage and replaced the whole answer with
            // its fallback message. That message is not the verdict that was asked for: it is treated like an
            // empty answer — nothing saved, charged or published, and the player may retry.
            if (grounded.CopyrightSanitized)
            {
                throw NoVerdict(request.SessionId, "the copyright guard replaced the answer with its fallback message");
            }

            return new VerdictAnswer(grounded.Answer, grounded.Citations, BasisOf(grounded));
        }
        catch (OperationCanceledException) when (BudgetExpired(budget, cancellationToken))
        {
            return await FallBackWithoutTheRulebookAsync(session, request, cancellationToken).ConfigureAwait(false);
        }
        catch (GroundedSessionGenerationException) when (BudgetExpired(budget, cancellationToken))
        {
            // A generation that failed because the budget cut it (some clients report a cancelled call as a failure).
            return await FallBackWithoutTheRulebookAsync(session, request, cancellationToken).ConfigureAwait(false);
        }
        catch (GroundedSessionGenerationException ex)
        {
            throw FailedCall(request.SessionId, ex.ErrorCode, ex.Message);
        }
    }

    /// <summary>
    /// What a grounded answer rests on. «Failed» and «found nothing» both leave no citation and are not the same
    /// thing; neither is a refusal, which says nothing from the sources and so cannot be cited.
    /// </summary>
    private static VerdictBasis BasisOf(GroundedSessionAnswerDto grounded)
    {
        if (grounded.RetrievalFailed)
        {
            return VerdictBasis.RetrievalUnavailable;
        }

        if (grounded.Citations.Count == 0)
        {
            return VerdictBasis.NoPassages;
        }

        return grounded.GroundingStatus == GroundingStatus.Ungrounded
            ? VerdictBasis.NotCovered
            : VerdictBasis.Rulebook;
    }

    private static bool BudgetExpired(CancellationTokenSource budget, CancellationToken callerToken)
        => budget.IsCancellationRequested && !callerToken.IsCancellationRequested;

    private async Task<VerdictAnswer> FallBackWithoutTheRulebookAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Rule dispute of SessionId={SessionId}: the grounded answer exceeded its {BudgetMs} ms budget; " +
            "the verdict is given without the rulebook",
            request.SessionId, _sessionAgentOptions.Value.ArbiterBudgetMs);
        MeepleAiMetrics.RecordRetrievalFallback(
            MeepleAiMetrics.RagFallbackTypes.RetrievalBudget, MeepleAiMetrics.RagFallbackSeverity.PartialLoss);

        return await AskTheModelAloneAsync(session, request, VerdictBasis.RetrievalUnavailable, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The rulebook is not in the prompt (no catalog game, the caller may not read it, or it could not be
    /// consulted in time): the model is asked directly, told it has none, and the verdict says so. It used to be
    /// told to «base its verdicts exclusively on the rulebook» and to quote it — which it did not have.
    /// </summary>
    private async Task<VerdictAnswer> AskTheModelAloneAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        VerdictBasis basis,
        CancellationToken cancellationToken)
    {
        var (systemPrompt, userPrompt) = ArbiterVerdictFormat.DirectPrompt(session, request.Description);

        var llmResult = await _llmService
            .GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            llmResult.Metadata.TryGetValue("error_code", out var errorCode);
            throw FailedCall(request.SessionId, errorCode, llmResult.ErrorMessage);
        }

        return new VerdictAnswer(
            llmResult.Response ?? string.Empty,
            Array.Empty<CitationDto>(),
            basis,
            FinishReason: llmResult.Metadata.TryGetValue("finish_reason", out var finishReason) ? finishReason : null,
            CompletionTokens: llmResult.Usage.CompletionTokens);
    }

    /// <summary>
    /// A call that failed. #4751: a refusal by the gateway (daily cap, quota, catalog) carries a code and a
    /// message the gateway wrote itself — «riprova tra poco» would be false for it, so both are passed on, as
    /// AskQuestionQueryHandler does. A provider that just did not answer (AI_PROVIDER_UNAVAILABLE) or any
    /// failure without a code keeps the generic message: the provider's own text stays in the log.
    /// </summary>
    private Exception FailedCall(Guid sessionId, string? errorCode, string? errorMessage)
    {
        _logger.LogError(
            "Arbitration failed for SessionId={SessionId}: {Error}",
            sessionId, errorMessage);

        if (!string.IsNullOrWhiteSpace(errorCode)
            && !string.Equals(errorCode, LlmAnswerErrorCodes.ProviderUnavailable, StringComparison.Ordinal))
        {
            return new LlmCallRefusedException(errorCode, errorMessage ?? NoVerdictMessage);
        }

        return new ExternalServiceException(NoVerdictMessage, NoVerdictErrorCode);
    }

    /// <summary>
    /// The failure of a call that «succeeded» without a usable verdict. The log carries what helps
    /// to tell a truncated answer (<c>finish_reason=length</c>, the reasoning budget eaten) from a
    /// refusal; the response carries only the fixed message.
    /// </summary>
    private ExternalServiceException NoVerdict(
        Guid sessionId, string reason, string? finishReason = null, int? completionTokens = null)
    {
        _logger.LogError(
            "Arbitration produced no verdict for SessionId={SessionId}: {Reason} (FinishReason={FinishReason}, CompletionTokens={CompletionTokens})",
            sessionId,
            reason,
            finishReason ?? "n/d",
            completionTokens?.ToString(CultureInfo.InvariantCulture) ?? "n/d");

        return new ExternalServiceException(NoVerdictMessage, NoVerdictErrorCode);
    }

    // ── DB Persistence ─────────────────────────────────────────────────────────

    private async Task PersistDisputesToDbAsync(
        LiveGameSession session,
        CancellationToken cancellationToken)
    {
        // #3882: .AsTracking() richiesto — il default del DbContext e' NoTracking (PERF-06),
        // quindi senza di esso questa lettura e' DETACHED: le mutazioni sotto non raggiungono
        // il change tracker e SaveChangesAsync non scrive, e non solleva.
        var entity = await _dbContext.LiveGameSessions
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogWarning(
                "LiveGameSessionEntity not found in DB for SessionId={SessionId}. " +
                "DisputesJson will not be persisted to database.",
                session.Id);
            return;
        }

        entity.DisputesJson = JsonSerializer.Serialize(session.Disputes, JsonOptions);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
