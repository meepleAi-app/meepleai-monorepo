using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CitationDto = Api.Models.CitationDto;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Command for submitting a rule dispute during a live game session.
/// The handler asks for a verdict that starts from the game's rulebook (KnowledgeBase's grounded
/// path, via <see cref="IMediator"/>), parses it, stores the <see cref="RuleDisputeEntry"/> on the
/// session, and broadcasts a <see cref="DisputeResolvedEvent"/>.
/// Game Night Improvvisata — E3: Arbitro mode.
/// </summary>
public sealed record SubmitRuleDisputeCommand(
    Guid SessionId,
    Guid CallerUserId,
    string Description,
    string RaisedByPlayerName) : IRequest<RuleDisputeResponse>;

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
///   3. Ask for a verdict. With a catalog game, through KnowledgeBase's grounded path
///      (<see cref="AskGroundedSessionQuery"/>, ADR-090): retrieval on the game's rulebook, copyright
///      tiers, leak guard. Without one there is no rulebook, and the model is asked directly.
///   4. Parse the VERDETTO / NOTA sections of the answer.
///   5. Persist <see cref="RuleDisputeEntry"/> to the session and DB.
///   6. Publish <see cref="DisputeResolvedEvent"/> for SignalR broadcast.
/// </summary>
/// <remarks>
/// #4751: the verdict is saved and sent to the whole table, so it is written at the most restrictive
/// copyright tier (<c>UserId = Guid.Empty</c>: verbatim text only for a free licence or an unprotected
/// category), never at the tier of whoever asked. The rule references are the passages that were
/// retrieved, not text the model wrote: without a rulebook the verdict says so instead of citing.
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

    /// <summary>
    /// Opens a verdict that does not rest on the rulebook (no game, no indexed rulebook, nothing in it
    /// that covers the case). It is part of the verdict itself, because the verdict and its references
    /// are what the whole table receives: a note kept in the response would reach only who asked.
    /// </summary>
    internal const string NotFromRulebookNotice =
        "Attenzione: questo verdetto non si basa sul regolamento (nessun passaggio pertinente), " +
        "ma sul buon senso e sulle regole generali del gioco.";

    private const int MaxRuleReferences = 3;
    private const int MaxQuotedChars = 280;

    private const string ArbiterPersona =
        "Sei l'ARBITRO ufficiale di una partita da tavolo. " +
        "Il tuo compito è emettere verdetti chiari, precisi e imparziali sulle dispute di regole. " +
        "Rispondi SEMPRE in italiano nel formato strutturato richiesto.";

    private const string VerdictFormat =
        "Rispondi in formato strutturato:\n" +
        "VERDETTO: [Chi ha ragione e perché — 1-2 frasi]\n" +
        "NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]";

    /// <summary>Wire value of a Full-tier citation (<c>GroundedAnswerService</c> maps the tier name, lower-cased).</summary>
    private const string FullTier = "full";

    // For Protected sources the model is told to put [ref:documentId:pageNum] before each paraphrase
    // (RagPromptAssemblyService): machine syntax, not something to show the table.
    private static readonly Regex CopyrightMarker = new(
        @"[ \t]*\[ref:[^:\]]+:\d+\][ \t]*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILlmService _llmService;
    private readonly IMediator _mediator;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitRuleDisputeCommandHandler> _logger;

    public SubmitRuleDisputeCommandHandler(
        ILiveSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILlmService llmService,
        IMediator mediator,
        ITierEnforcementService tierEnforcementService,
        IPublisher publisher,
        TimeProvider timeProvider,
        ILogger<SubmitRuleDisputeCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
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

        // 5–6. Ask for the verdict: from the game's rulebook when there is one, else from the model alone
        var answer = await AskForVerdictAsync(session, request, cancellationToken).ConfigureAwait(false);

        // #4751: a model that answers with nothing has not given a verdict. It used to be parsed into
        // «Nessun verdetto disponibile.», saved as the verdict, broadcast and counted against the quota.
        // An empty answer is a failure like !Success: nothing is saved, charged or published, and the
        // caller gets a readable 503 (the model's error text goes to the log, never to the response).
        var text = StripCopyrightMarkers(answer.Text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw NoVerdict(request.SessionId, answer, "the model answered with no text");
        }

        _logger.LogDebug(
            "Arbitro response for SessionId={SessionId}: {Response}",
            request.SessionId, text);

        // 7. Parse the structured response
        var (verdict, note) = ParseArbitrationResponse(text);

        // The bare skeleton («VERDETTO:\nNOTA:», or an answer cut right after a header) parses to
        // an empty verdict: same case as an empty answer, not a verdict to save.
        if (string.IsNullOrWhiteSpace(verdict))
        {
            throw NoVerdict(request.SessionId, answer, "the answer holds only empty section headers");
        }

        // #4751: the references are the passages that were retrieved — never text the model wrote, which
        // is a quote it cannot have. A verdict that does not rest on the rulebook says so, in the verdict
        // itself, and cites nothing.
        var ruleReferences = new List<string>();
        if (answer.RestsOnTheRulebook)
        {
            ruleReferences = BuildRuleReferences(answer.Citations);
        }
        else
        {
            verdict = $"{NotFromRulebookNotice} {verdict}";
        }

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
            "Rule dispute resolved: SessionId={SessionId}, DisputeId={DisputeId}, Verdict={Verdict}",
            session.Id, entry.Id, verdict);

        return new RuleDisputeResponse(
            Id: entry.Id,
            Verdict: entry.Verdict,
            RuleReferences: entry.RuleReferences,
            Note: note);
    }

    // ── Asking for the verdict ─────────────────────────────────────────────────

    /// <summary>What came back from the model, and whether the rulebook is what it rests on.</summary>
    /// <param name="Text">The raw answer, markers and all.</param>
    /// <param name="Citations">The passages retrieved, after KnowledgeBase's tier-nulling; empty on the direct path.</param>
    /// <param name="RestsOnTheRulebook">True only when a passage was retrieved and the answer is not the refusal.</param>
    /// <param name="FinishReason">Only the direct path reports it; for the log.</param>
    /// <param name="CompletionTokens">Only the direct path reports it; for the log.</param>
    private sealed record VerdictAnswer(
        string Text,
        IReadOnlyList<CitationDto> Citations,
        bool RestsOnTheRulebook,
        string? FinishReason = null,
        int? CompletionTokens = null);

    private Task<VerdictAnswer> AskForVerdictAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
        => session.GameId is { } gameId
            ? AskTheRulebookAsync(session, request, gameId, cancellationToken)
            : AskTheModelAloneAsync(session, request, cancellationToken);

    /// <summary>
    /// The game has a catalog id: KnowledgeBase retrieves from its rulebook and finalizes the answer
    /// (copyright tiers, leak guard, citation mapping — ADR-090). <c>UserId = Guid.Empty</c> is the most
    /// restrictive tier: the verdict is saved and sent to every player at the table, so it must not be
    /// written at the level of whoever asked (who may have uploaded the file and own the game).
    /// </summary>
    private async Task<VerdictAnswer> AskTheRulebookAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        Guid gameId,
        CancellationToken cancellationToken)
    {
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
                        ResponseInstructions: BuildResponseInstructions(session)),
                    cancellationToken)
                .ConfigureAwait(false);

            // Grounded only if a passage was retrieved and the answer is not the refusal (a refusal
            // «does not say anything from the sources», so the passages it came with are not its basis).
            var restsOnTheRulebook = grounded.GroundingStatus != GroundingStatus.Ungrounded
                && grounded.Citations.Count > 0;

            return new VerdictAnswer(grounded.Answer, grounded.Citations, restsOnTheRulebook);
        }
        catch (GroundedSessionGenerationException ex)
        {
            throw FailedCall(request.SessionId, ex.ErrorCode, ex.Message);
        }
    }

    /// <summary>
    /// No catalog game (an improvvisata session): there is no rulebook to retrieve from, so the model is
    /// asked directly, told it has none, and the verdict says so. It used to be told to «base its verdicts
    /// exclusively on the rulebook» and to quote it — which it did not have.
    /// </summary>
    private async Task<VerdictAnswer> AskTheModelAloneAsync(
        LiveGameSession session,
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var (systemPrompt, userPrompt) = BuildDirectPrompt(session, request);

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
            RestsOnTheRulebook: false,
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
    private ExternalServiceException NoVerdict(Guid sessionId, VerdictAnswer answer, string reason)
    {
        _logger.LogError(
            "Arbitration produced no verdict for SessionId={SessionId}: {Reason} (FinishReason={FinishReason}, CompletionTokens={CompletionTokens})",
            sessionId,
            reason,
            answer.FinishReason ?? "n/d",
            answer.CompletionTokens?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/d");

        return new ExternalServiceException(NoVerdictMessage, NoVerdictErrorCode);
    }

    // ── Prompt Building ────────────────────────────────────────────────────────

    /// <summary>
    /// For the grounded path: appended to the system prompt, after the rulebook passages. Not a
    /// request for a quote, and no REGOLA section: the references are added from the passages found.
    /// </summary>
    private static string BuildResponseInstructions(LiveGameSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Modalità Arbitro");
        builder.AppendLine(ArbiterPersona);
        builder.AppendLine(
            "Basa il verdetto sui passaggi del regolamento forniti sopra. " +
            "Se non coprono il caso, dillo e proponi l'interpretazione più ragionevole.");
        builder.AppendLine(
            "Non scrivere citazioni tra virgolette né riferimenti a pagina o sezione: " +
            "li aggiunge il sistema, solo da passaggi davvero trovati.");
        builder.AppendLine(BuildContext(session));
        builder.Append(VerdictFormat);
        return builder.ToString();
    }

    /// <summary>For the direct path: there is no rulebook, and the model is told so.</summary>
    private static (string SystemPrompt, string UserPrompt) BuildDirectPrompt(
        LiveGameSession session,
        SubmitRuleDisputeCommand request)
    {
        var systemPrompt =
            ArbiterPersona + " " +
            "Non hai accesso al regolamento di questo gioco: basa il verdetto sul buon senso e sulle regole " +
            "generali del gioco, dichiaralo con chiarezza e non citare pagine, sezioni o testo del regolamento.";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("MODALITÀ ARBITRO. Emetti un VERDETTO chiaro su questa disputa.");
        userPrompt.AppendLine();
        userPrompt.Append("Disputa: \"").Append(request.Description).AppendLine("\"");
        userPrompt.AppendLine(BuildContext(session));
        userPrompt.AppendLine();
        userPrompt.Append(VerdictFormat);

        return (systemPrompt, userPrompt.ToString());
    }

    /// <summary>The session context both prompts carry: game, players, turn and the latest verdicts.</summary>
    private static string BuildContext(LiveGameSession session)
    {
        var playerNames = session.Players.Count > 0
            ? string.Join(", ", session.Players.Where(p => p.IsActive).Select(p => p.DisplayName))
            : "N/D";

        var context = new StringBuilder();
        context
            .Append("Contesto: Sessione \"").Append(session.GameName)
            .Append("\" con giocatori: ").Append(playerNames)
            .Append(". Turno: ").Append(session.CurrentTurnIndex).Append('.');

        var previousDisputesSummary = BuildPreviousDisputesSummary(session.Disputes);
        if (!string.IsNullOrEmpty(previousDisputesSummary))
        {
            context.AppendLine().Append("Verdetti precedenti: ").Append(previousDisputesSummary);
        }

        return context.ToString();
    }

    private static string BuildPreviousDisputesSummary(IReadOnlyList<RuleDisputeEntry> disputes)
    {
        if (disputes.Count == 0)
            return string.Empty;

        // Only include the last 3 disputes to keep the prompt concise
        var recent = disputes.TakeLast(3).ToList();
        var parts = recent.Select((d, i) =>
            $"{i + 1}. \"{d.Description}\" → {d.Verdict}");

        return string.Join("; ", parts);
    }

    // ── Response Parsing ───────────────────────────────────────────────────────

    /// <summary>
    /// The VERDETTO and NOTA sections. #4751: REGOLA is recognized only to be left out — the model is no
    /// longer asked for it (a quote it cannot have), and what it writes there is neither the verdict nor
    /// the note nor a reference: those come from the passages that were retrieved.
    /// </summary>
    internal static (string Verdict, string? Note) ParseArbitrationResponse(string response)
    {
        // #4751: the handler turns an empty answer into a failure before it gets here. This used to
        // invent «Nessun verdetto disponibile.», which the handler then saved as a real verdict; reaching
        // this point empty is a bug in the caller, so it fails loudly instead of producing one.
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        var lines = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();

        var verdict = ExtractSection(lines, "VERDETTO:");
        var nota = ExtractSection(lines, "NOTA:");

        // No VERDETTO text: an unstructured answer is the verdict itself, minus any REGOLA section. The
        // bare skeleton («VERDETTO:\nREGOLA:\nNOTA:», or an answer cut right after a header) is not an
        // answer at all, and stays empty so that the handler can refuse it.
        if (string.IsNullOrWhiteSpace(verdict))
        {
            var withoutRule = WithoutRuleSection(lines);
            verdict = withoutRule.All(IsSectionHeaderOnly)
                ? string.Empty
                : withoutRule.Count == lines.Count ? response.Trim() : string.Join("\n", withoutRule);
        }

        return (verdict, string.IsNullOrWhiteSpace(nota) ? null : nota);
    }

    /// <summary>The lines of an unstructured answer without the REGOLA section (its header and what follows, up to the next header).</summary>
    private static List<string> WithoutRuleSection(List<string> lines)
    {
        var kept = new List<string>(lines.Count);
        var inRuleSection = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("REGOLA:", StringComparison.OrdinalIgnoreCase))
            {
                inRuleSection = true;
                continue;
            }

            if (inRuleSection && SectionHeaders.Any(h => line.StartsWith(h, StringComparison.OrdinalIgnoreCase)))
            {
                inRuleSection = false;
            }

            if (!inRuleSection)
            {
                kept.Add(line);
            }
        }

        return kept;
    }

    /// <summary>Takes the machine syntax of the copyright paraphrase (<c>[ref:documentId:pageNum]</c>) out of the text.</summary>
    private static string StripCopyrightMarkers(string text)
        => CopyrightMarker.Replace(text, " ");

    // ── Rule references ────────────────────────────────────────────────────────

    /// <summary>
    /// The references of a verdict that rests on the rulebook: the most relevant retrieved passages, one
    /// per page, as a page and — only for a Full-tier passage — its text. A Protected passage is cited by
    /// page alone: the verdict is the paraphrase, and the tier, not the presence of a snippet, decides
    /// what may be quoted (the shared finalize step nulls a Protected preview; this does not rely on it).
    /// </summary>
    internal static List<string> BuildRuleReferences(IReadOnlyList<CitationDto> citations)
        => citations
            .OrderByDescending(c => c.RelevanceScore)
            .DistinctBy(c => (c.DocumentId, c.PageNumber))
            .Take(MaxRuleReferences)
            .Select(ToRuleReference)
            .ToList();

    private static string ToRuleReference(CitationDto citation)
    {
        var label = citation.PageNumber > 0 ? $"Regolamento, p. {citation.PageNumber}" : "Regolamento";

        if (string.Equals(citation.CopyrightTier, FullTier, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(citation.SnippetPreview))
        {
            return $"{label}: «{Shorten(citation.SnippetPreview)}»";
        }

        return label;
    }

    private static string Shorten(string text)
    {
        var collapsed = Whitespace.Replace(text, " ").Trim();
        return collapsed.Length <= MaxQuotedChars ? collapsed : collapsed[..MaxQuotedChars].TrimEnd() + "…";
    }

    private static readonly string[] SectionHeaders = { "VERDETTO:", "REGOLA:", "NOTA:" };

    /// <summary>A line that is nothing but a section header, with no text after it.</summary>
    private static bool IsSectionHeaderOnly(string line)
    {
        foreach (var header in SectionHeaders)
        {
            if (line.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(line[header.Length..]);
            }
        }

        return string.IsNullOrWhiteSpace(line);
    }

    private static string ExtractSection(List<string> lines, string prefix)
    {
        var parts = new List<string>();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                var remainder = line[prefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(remainder))
                    parts.Add(remainder);
                continue;
            }

            if (capturing)
            {
                // Stop at the next recognised section header
                if (line.StartsWith("VERDETTO:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("REGOLA:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("NOTA:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                parts.Add(line);
            }
        }

        return string.Join(" ", parts).Trim();
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
